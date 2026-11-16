using System.Net;
using System.Net.Sockets;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Inspecta.Infrastructure.Mail;

/// <summary>
/// Tries a mailbox the way the app uses it, with nothing changed in it: connect to the server, sign in, open the inbox read-only, and sign in
/// to the sending server (no message is sent). Servers come from user input, so only public addresses are ever connected to.
/// </summary>
public sealed class MailKitMailboxProbe : IMailboxProbe
{
    private static readonly string[] Order = ["reach", "sign_in", "read", "send"];
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(15);

    public async Task<IReadOnlyList<MailboxCheckStep>> ProbeAsync(MailboxProbeSettings settings, CancellationToken cancellationToken = default)
    {
        var steps = new List<MailboxCheckStep>();
        using var imap = new ImapClient();
        try
        {
            var options = settings.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            if (!await RunAsync("reach", settings.ImapServer, settings.ImapPort, steps, token => ConnectAsync(imap, settings.ImapServer, settings.ImapPort, options, token), cancellationToken))
                return Complete(steps);
            if (!await RunAsync("sign_in", settings.ImapServer, settings.ImapPort, steps, token => AuthenticateAsync(imap, settings, token), cancellationToken))
                return Complete(steps);
            if (!await RunAsync("read", settings.ImapServer, settings.ImapPort, steps, token => imap.Inbox.OpenAsync(FolderAccess.ReadOnly, token), cancellationToken))
                return Complete(steps);

            using var smtp = new SmtpClient();
            var smtpOptions = settings.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await RunAsync("send", settings.SmtpServer, settings.SmtpPort, steps, async token =>
            {
                await ConnectAsync(smtp, settings.SmtpServer, settings.SmtpPort, smtpOptions, token);
                await AuthenticateAsync(smtp, settings, token);
            }, cancellationToken);
            return Complete(steps);
        }
        finally
        {
            try { if (imap.IsConnected) await imap.DisconnectAsync(true, CancellationToken.None); }
            catch (Exception) { /* closing a test connection never matters */ }
        }
    }

    /// <summary>Runs one step; a failure is recorded in words and stops the test.</summary>
    private static async Task<bool> RunAsync(
        string step, string host, int port, List<MailboxCheckStep> steps, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(StepTimeout);
        try
        {
            await action(limit.Token);
            steps.Add(new MailboxCheckStep(step, "ok"));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = exception is OperationCanceledException ? new IOException("timeout", new TimeoutException()) : exception;
            var (message, hint) = MailboxProbeFailures.Describe(step, failure, host, port);
            steps.Add(new MailboxCheckStep(step, "failed", message, hint));
            return false;
        }
    }

    private static IReadOnlyList<MailboxCheckStep> Complete(List<MailboxCheckStep> steps)
    {
        foreach (var step in Order.Skip(steps.Count)) steps.Add(new MailboxCheckStep(step, "skipped"));
        return steps;
    }

    private static Task AuthenticateAsync(IMailService client, MailboxProbeSettings settings, CancellationToken cancellationToken) =>
        settings.AccessToken is { Length: > 0 } token
            ? client.AuthenticateAsync(new SaslMechanismOAuth2(settings.Username, token), cancellationToken)
            : client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);

    private static async Task ConnectAsync(IMailService client, string host, int port, SecureSocketOptions options, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(PublicAddressGuard.IsNonPublic))
            throw new InvalidOperationException("The server must resolve only to public IP addresses.");

        Exception? last = null;
        foreach (var address in addresses.OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                await client.ConnectAsync(socket, host, port, options, cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                socket.Dispose();
                last = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new IOException($"Could not connect to {host}:{port}.", last);
    }
}
