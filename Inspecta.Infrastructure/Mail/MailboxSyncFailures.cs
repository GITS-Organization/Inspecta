using System.Net.Sockets;
using MailKit.Security;

namespace Inspecta.Infrastructure.Mail;

/// <summary>Why a sync of a mailbox failed, when the reason is the mailbox itself (and not, say, one email that could not be imported).</summary>
public sealed record MailboxSyncFailure(string Reason, string Step);

public static class MailboxSyncFailures
{
    private const int MaxDetailLength = 200;

    /// <summary>
    /// A refused sign-in, a server that cannot be reached, or a server that is not a public address are remembered with their own advice. Any other
    /// failure that stops a sync is remembered too, as a problem reading the mailbox with what went wrong: otherwise the mailbox shows as connected
    /// while no email arrives and nobody can see why. Only a cancelled sync (the service stopping) says nothing and gives <c>null</c>.
    /// </summary>
    public static MailboxSyncFailure? Classify(Exception exception, string host, int port)
    {
        switch (exception)
        {
            case OperationCanceledException:
                return null;
            case AuthenticationException:
                return Of("sign_in", exception, host, port);
            case SocketException or IOException or SslHandshakeException:
                return Of("reach", exception, host, port);
            case InvalidOperationException { Message: var message } when message.Contains("public IP", StringComparison.Ordinal):
                return Of("reach", exception, host, port);
            default:
                return new MailboxSyncFailure($"The mailbox was reached but reading it stopped with an error: {Detail(exception)}", "read");
        }
    }

    /// <summary>One email could not be imported (it is tried again, then skipped), so the mailbox is not delivering everything.</summary>
    public static MailboxSyncFailure ForImport(long uid, Exception exception) =>
        new($"An email in the mailbox (number {uid}) could not be imported: {Detail(exception)}", "read");

    /// <summary>
    /// What went wrong, short enough to show on a card: the type and the first line of the deepest message (the database or the mail server says what
    /// the matter is there; the outer messages only say that something failed). Later lines are left out, as they can carry data.
    /// </summary>
    private static string Detail(Exception exception)
    {
        var root = exception;
        while (root.InnerException is { } inner) root = inner;
        var line = (root.Message ?? string.Empty).Split('\n', 2)[0].Trim();
        var text = $"{root.GetType().Name}: {line}";
        return text.Length > MaxDetailLength ? text[..MaxDetailLength] : text;
    }

    private static MailboxSyncFailure Of(string step, Exception exception, string host, int port)
    {
        var (message, hint) = MailboxProbeFailures.Describe(step, exception, host, port);
        return new MailboxSyncFailure($"{message} {hint}".Trim(), step);
    }
}
