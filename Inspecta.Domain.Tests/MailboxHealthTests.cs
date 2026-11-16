using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>What a mailbox remembers about whether it works: the last check, the last email, and why it stopped working.</summary>
public sealed class MailboxHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static CoordinatorEmailConfig Password() =>
        CoordinatorEmailConfig.Create(Guid.NewGuid(), Guid.NewGuid(), "imap.example.com", 993, "a@example.com", "encrypted", true);

    private static CoordinatorEmailConfig Microsoft() =>
        CoordinatorEmailConfig.CreateMicrosoft(Guid.NewGuid(), Guid.NewGuid(), "a@example.com", "token", "tenant");

    [Fact]
    public void A_new_mailbox_has_never_been_checked_and_has_received_nothing()
    {
        var config = Password();

        Assert.Null(config.LastCheckedAt);
        Assert.Null(config.LastEmailAt);
        Assert.Null(config.LastFailedStep);
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
    }

    [Fact]
    public void A_successful_check_is_remembered_and_clears_any_old_problem_even_a_sending_one()
    {
        var config = Password();
        config.RecordFailedCheck(Now.AddHours(-1), "We can read the mailbox but cannot send from it.", "send");

        config.RecordSuccessfulCheck(Now);

        Assert.Equal(Now, config.LastCheckedAt);
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
        Assert.Null(config.LastFailedStep);
    }

    [Theory]
    [InlineData("reach")]
    [InlineData("sign_in")]
    [InlineData("read")]
    [InlineData("send")]
    public void A_failed_check_is_a_problem_with_the_reason_and_the_step(string step)
    {
        var config = Password();

        config.RecordFailedCheck(Now, "Something did not work.", step);

        Assert.Equal(MailboxConnectionStatus.Problem, config.ConnectionStatus);
        Assert.Equal("Something did not work.", config.LastAuthError);
        Assert.Equal(step, config.LastFailedStep);
        Assert.Equal(Now, config.LastCheckedAt);
    }

    [Fact]
    public void One_refused_imap_sign_in_of_a_microsoft_mailbox_is_a_problem_not_a_reconnect()
    {
        var config = Microsoft();

        config.RecordFailedCheck(Now, "The mailbox refused the email or password.", "sign_in");

        Assert.Equal(MailboxConnectionStatus.Problem, config.ConnectionStatus);
    }

    [Fact]
    public void A_reconnect_that_is_waiting_is_never_replaced_by_a_lesser_problem()
    {
        var config = Microsoft();
        config.MarkNeedsReconnect("Microsoft refused the saved sign-in.");

        config.RecordFailedCheck(Now, "We could not reach the server.", "reach");

        Assert.Equal(MailboxConnectionStatus.NeedsReconnect, config.ConnectionStatus);
        Assert.Equal("Microsoft refused the saved sign-in.", config.LastAuthError);
    }

    [Fact]
    public void The_last_email_only_moves_forward()
    {
        var config = Password();

        config.RecordEmailReceived(Now.AddDays(-5));
        config.RecordEmailReceived(Now.AddDays(-3));
        config.RecordEmailReceived(Now.AddDays(-4));

        Assert.Equal(Now.AddDays(-3), config.LastEmailAt);
    }

    [Fact]
    public void A_date_in_the_future_in_an_email_header_cannot_freeze_the_last_email()
    {
        var config = Password();

        config.RecordEmailReceived(DateTimeOffset.UtcNow.AddYears(70));

        Assert.True(config.LastEmailAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Saving_new_settings_clears_the_problem_the_reason_the_step_and_the_old_check()
    {
        var config = Password();
        config.RecordFailedCheck(Now, "The mailbox refused the email or password.", "sign_in");

        config.Update("imap.example.com", 993, "a@example.com", "encrypted-2", true);

        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
        Assert.Null(config.LastFailedStep);
        Assert.Null(config.LastCheckedAt);
    }

    [Fact]
    public void A_sync_that_signed_in_clears_a_problem_with_reaching_or_signing_in()
    {
        var config = Password();
        config.RecordFailedCheck(Now.AddMinutes(-3), "We could not reach the server.", "reach");

        var changed = config.RecordSyncSuccess(Now);

        Assert.True(changed);
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
        Assert.Equal(Now, config.LastCheckedAt);
    }

    [Fact]
    public void A_sync_never_clears_a_sending_problem_because_a_sync_does_not_send()
    {
        var config = Password();
        config.RecordFailedCheck(Now.AddMinutes(-3), "We can read the mailbox but cannot send from it.", "send");

        config.RecordSyncSuccess(Now);

        Assert.Equal(MailboxConnectionStatus.Problem, config.ConnectionStatus);
        Assert.Equal("send", config.LastFailedStep);
        Assert.Equal("We can read the mailbox but cannot send from it.", config.LastAuthError);
    }

    [Fact]
    public void A_healthy_mailbox_is_not_written_again_on_every_sync_only_now_and_then()
    {
        var config = Password();
        Assert.True(config.RecordSyncSuccess(Now));

        Assert.False(config.RecordSyncSuccess(Now.AddMinutes(2)));
        Assert.Equal(Now, config.LastCheckedAt);
        Assert.True(config.RecordSyncSuccess(Now.AddMinutes(20)));
        Assert.Equal(Now.AddMinutes(20), config.LastCheckedAt);
    }

    [Fact]
    public void The_same_failure_is_not_written_again_on_every_sync_but_a_new_one_is()
    {
        var config = Password();
        Assert.True(config.RecordSyncFailure(Now, "We could not reach the server.", "reach"));

        Assert.False(config.RecordSyncFailure(Now.AddMinutes(2), "We could not reach the server.", "reach"));
        Assert.True(config.RecordSyncFailure(Now.AddMinutes(3), "The mailbox refused the email or password.", "sign_in"));
        Assert.Equal("sign_in", config.LastFailedStep);
    }

    [Fact]
    public void A_sync_failure_never_replaces_a_sending_problem_so_a_later_good_sync_cannot_hide_it()
    {
        var config = Password();
        config.RecordFailedCheck(Now, "We can read the mailbox but cannot send from it.", "send");

        config.RecordSyncFailure(Now.AddMinutes(20), "We could not reach the server.", "reach");
        config.RecordSyncSuccess(Now.AddMinutes(40));

        Assert.Equal(MailboxConnectionStatus.Problem, config.ConnectionStatus);
        Assert.Equal("send", config.LastFailedStep);
        Assert.Equal("We can read the mailbox but cannot send from it.", config.LastAuthError);
    }

    [Fact]
    public void A_different_mailbox_starts_without_the_last_email_of_the_old_one_but_the_same_mailbox_keeps_it()
    {
        var config = Password();
        config.RecordEmailReceived(Now.AddDays(-1));

        config.Update("imap.example.com", 993, "a@example.com", "encrypted-2", true);
        Assert.Equal(Now.AddDays(-1), config.LastEmailAt);

        config.Update("imap.other.com", 993, "b@other.com", "encrypted-3", true);
        Assert.Null(config.LastEmailAt);
    }
}
