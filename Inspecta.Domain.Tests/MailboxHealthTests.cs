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
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
    }

    [Fact]
    public void A_successful_check_is_remembered_and_clears_an_old_problem()
    {
        var config = Password();
        config.RecordFailedCheck(Now.AddHours(-1), "The mailbox refused the email or password.", signInRefused: true);

        config.RecordSuccessfulCheck(Now);

        Assert.Equal(Now, config.LastCheckedAt);
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
    }

    [Fact]
    public void A_failed_check_of_a_password_mailbox_is_a_problem_with_the_reason()
    {
        var config = Password();

        config.RecordFailedCheck(Now, "The mailbox refused the email or password.", signInRefused: true);

        Assert.Equal(MailboxConnectionStatus.Problem, config.ConnectionStatus);
        Assert.Equal("The mailbox refused the email or password.", config.LastAuthError);
        Assert.Equal(Now, config.LastCheckedAt);
    }

    [Fact]
    public void Microsoft_refusing_the_sign_in_means_the_coordinator_must_connect_again()
    {
        var config = Microsoft();

        config.RecordFailedCheck(Now, "Microsoft refused the saved sign-in.", signInRefused: true);

        Assert.Equal(MailboxConnectionStatus.NeedsReconnect, config.ConnectionStatus);
    }

    [Fact]
    public void A_server_that_cannot_be_reached_is_a_problem_even_for_a_microsoft_mailbox_not_a_reconnect()
    {
        var config = Microsoft();

        config.RecordFailedCheck(Now, "We could not reach the server.", signInRefused: false);

        Assert.Equal(MailboxConnectionStatus.Problem, config.ConnectionStatus);
    }

    [Fact]
    public void A_reconnect_in_waiting_is_not_hidden_by_a_later_unrelated_failure()
    {
        var config = Microsoft();
        config.RecordFailedCheck(Now, "Microsoft refused the saved sign-in.", signInRefused: true);

        config.RecordFailedCheck(Now.AddMinutes(5), "We could not reach the server.", signInRefused: false);

        Assert.Equal(MailboxConnectionStatus.NeedsReconnect, config.ConnectionStatus);
    }

    [Fact]
    public void The_last_email_only_moves_forward()
    {
        var config = Password();

        config.RecordEmailReceived(Now);
        config.RecordEmailReceived(Now.AddDays(-3));

        Assert.Equal(Now, config.LastEmailAt);
    }

    [Fact]
    public void Saving_new_settings_clears_the_problem_and_the_old_reason()
    {
        var config = Password();
        config.RecordFailedCheck(Now, "The mailbox refused the email or password.", signInRefused: true);

        config.Update("imap.example.com", 993, "a@example.com", "encrypted-2", true);

        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
    }
}
