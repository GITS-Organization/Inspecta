using System.Net.Sockets;
using Inspecta.Application.Interfaces;
using Inspecta.Infrastructure.Mail;
using MailKit.Security;

namespace Inspecta.Domain.Tests;

/// <summary>What a person is told when a mailbox step fails, and that the probe never reaches a private address.</summary>
public sealed class MailboxProbeTests
{
    [Fact]
    public void A_refused_password_at_gmail_says_it_needs_an_app_password()
    {
        var (message, hint) = MailboxProbeFailures.Describe("sign_in", new AuthenticationException("bad"), "imap.gmail.com", 993);

        Assert.Equal("The mailbox refused the email or password.", message);
        Assert.Contains("App password", hint);
    }

    [Fact]
    public void A_refused_password_elsewhere_gets_the_general_advice()
    {
        var (message, hint) = MailboxProbeFailures.Describe("sign_in", new AuthenticationException("bad"), "imap.example.com", 993);

        Assert.Equal("The mailbox refused the email or password.", message);
        Assert.DoesNotContain("App password", hint);
        Assert.Contains("password", hint);
    }

    [Fact]
    public void A_refused_password_at_microsoft_points_to_connecting_outlook()
    {
        var (_, hint) = MailboxProbeFailures.Describe("sign_in", new AuthenticationException("bad"), "outlook.office365.com", 993);

        Assert.Contains("Outlook", hint);
    }

    [Fact]
    public void A_server_name_that_does_not_exist_is_named_in_the_message()
    {
        var (message, hint) = MailboxProbeFailures.Describe("reach", new SocketException((int)SocketError.HostNotFound), "imap.nowhere.test", 993);

        Assert.Contains("imap.nowhere.test", message);
        Assert.Contains("name", hint);
    }

    [Fact]
    public void A_server_that_does_not_answer_names_the_server_and_the_port()
    {
        var (message, hint) = MailboxProbeFailures.Describe("reach", new IOException("x", new TimeoutException()), "imap.example.com", 143);

        Assert.Contains("imap.example.com:143", message);
        Assert.Contains("port", hint);
    }

    [Fact]
    public void A_private_address_is_refused_with_its_own_message()
    {
        var (message, _) = MailboxProbeFailures.Describe("reach", new InvalidOperationException("IMAP server must resolve only to public IP addresses."), "intranet.local", 993);

        Assert.Contains("public", message);
    }

    [Fact]
    public void A_failure_while_reading_the_inbox_says_imap_may_be_off()
    {
        var (message, hint) = MailboxProbeFailures.Describe("read", new InvalidOperationException("x"), "imap.example.com", 993);

        Assert.Contains("inbox", message);
        Assert.Contains("IMAP", hint);
    }

    [Fact]
    public void A_failure_while_sending_names_the_sending_server()
    {
        var (message, hint) = MailboxProbeFailures.Describe("send", new IOException("x"), "smtp.example.com", 587);

        Assert.Contains("smtp.example.com:587", message);
        Assert.Contains("Advanced", hint);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task The_probe_never_connects_to_a_private_address_and_skips_the_rest(string host)
    {
        var probe = new MailKitMailboxProbe();

        var steps = await probe.ProbeAsync(new MailboxProbeSettings(host, 993, true, "a@example.com", "pw", null, host, 587, false));

        Assert.Equal(["reach", "sign_in", "read", "send"], steps.Select(step => step.Step));
        Assert.Equal(["failed", "skipped", "skipped", "skipped"], steps.Select(step => step.State));
        Assert.Contains("public", steps[0].Message);
    }

    [Fact]
    public void A_sync_that_the_server_refused_is_a_refused_sign_in_with_the_reason_and_the_advice()
    {
        var failure = MailboxSyncFailures.Classify(new AuthenticationException("bad"), "imap.gmail.com", 993);

        Assert.NotNull(failure);
        Assert.True(failure!.SignInRefused);
        Assert.StartsWith("The mailbox refused the email or password.", failure.Reason);
        Assert.Contains("App password", failure.Reason);
    }

    [Theory]
    [InlineData(typeof(SocketException))]
    [InlineData(typeof(IOException))]
    public void A_sync_that_could_not_reach_the_server_is_a_problem_but_not_a_refused_sign_in(Type kind)
    {
        var error = (Exception)(kind == typeof(SocketException) ? new SocketException((int)SocketError.ConnectionRefused) : new IOException("x"));

        var failure = MailboxSyncFailures.Classify(error, "imap.example.com", 993);

        Assert.NotNull(failure);
        Assert.False(failure!.SignInRefused);
        Assert.Contains("imap.example.com:993", failure.Reason);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(OperationCanceledException))]
    public void A_failure_that_is_not_about_the_mailbox_never_marks_the_mailbox_as_broken(Type kind)
    {
        var error = (Exception)Activator.CreateInstance(kind, "importing one email failed")!;

        Assert.Null(MailboxSyncFailures.Classify(error, "imap.example.com", 993));
    }
}
