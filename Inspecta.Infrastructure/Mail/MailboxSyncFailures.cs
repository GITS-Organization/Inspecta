using System.Net.Sockets;
using MailKit.Security;

namespace Inspecta.Infrastructure.Mail;

/// <summary>Why a sync of a mailbox failed, when the reason is the mailbox itself (and not, say, one email that could not be imported).</summary>
public sealed record MailboxSyncFailure(string Reason, string Step);

public static class MailboxSyncFailures
{
    /// <summary>
    /// A refused sign-in, a server that cannot be reached, or a server that is not a public address are problems of the mailbox and are remembered.
    /// Anything else (a cancelled sync, a bug in importing one email) says nothing about the mailbox and gives <c>null</c>.
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
                return null;
        }
    }

    private static MailboxSyncFailure Of(string step, Exception exception, string host, int port)
    {
        var (message, hint) = MailboxProbeFailures.Describe(step, exception, host, port);
        return new MailboxSyncFailure($"{message} {hint}".Trim(), step);
    }
}
