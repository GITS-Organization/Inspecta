using System.Net.Sockets;
using MailKit.Security;

namespace Inspecta.Infrastructure.Mail;

/// <summary>What a person is told when a step of a mailbox test fails: what happened, in plain words, and what to do. Only fixed texts reach the user.</summary>
public static class MailboxProbeFailures
{
    public static (string Message, string Hint) Describe(string step, Exception exception, string host, int port) => step switch
    {
        "reach" => Reach(exception, host, port),
        "sign_in" => ("The mailbox refused the email or password.", SignInHint(host)),
        "read" => ("We signed in but could not open the inbox.", "Check that IMAP is turned on for this mailbox in your email provider’s settings."),
        _ => Send(exception, host, port)
    };

    private static (string, string) Reach(Exception exception, string host, int port) => exception switch
    {
        InvalidOperationException { Message: var message } when message.Contains("public IP", StringComparison.Ordinal) =>
            ("The server must be a public address, not one inside a private network.", "Use the server name your email provider gives you."),
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain } =>
            ($"We could not find {host}.", "Check the spelling of the server name."),
        SslHandshakeException =>
            ("A secure connection to the server could not be established.", "Check the port and the SSL setting under Advanced settings."),
        _ => ($"We could not reach {host}:{port}.", "Check the server name and the port. Your email provider’s help page lists them.")
    };

    private static string SignInHint(string host)
    {
        var name = host.ToLowerInvariant();
        if (name.EndsWith("gmail.com", StringComparison.Ordinal) || name.EndsWith("googlemail.com", StringComparison.Ordinal))
            return "Gmail needs an App password (16 letters), not the normal password.";
        if (name.Contains("yahoo", StringComparison.Ordinal))
            return "Yahoo needs an App password, created in the account security page, not the normal password.";
        if (name.Contains("office365", StringComparison.Ordinal) || name.Contains("outlook", StringComparison.Ordinal))
            return "This is a Microsoft mailbox: ask the coordinator to connect Outlook from their own app instead of using a password.";
        return "Check the email and the password, then test again.";
    }

    private static (string, string) Send(Exception exception, string host, int port) => exception is AuthenticationException
        ? ($"The mailbox can be read, but sending was refused ({host}:{port}).", "Check that this account may send email, and the sending (SMTP) server and port under Advanced settings.")
        : ($"We can read the mailbox but cannot send from it ({host}:{port}).", "Check the sending (SMTP) server and port under Advanced settings. Without sending, quotations and certificates cannot be emailed.");
}
