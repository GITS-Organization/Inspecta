using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>Trying the mailbox settings of a request before they are saved.</summary>
public static class MailboxVerification
{
    /// <summary>What to try for typed settings: the sending server and port are worked out like the saved mailbox does it.</summary>
    public static MailboxProbeSettings SettingsOf(CoordinatorEmailConfigRequest request)
    {
        var imapServer = request.ImapServer.Trim();
        var smtpServer = string.IsNullOrWhiteSpace(request.SmtpServer) ? CoordinatorEmailConfig.DeriveSmtpServer(imapServer) : request.SmtpServer.Trim();
        var smtpPort = request.SmtpPort ?? 587;
        return new MailboxProbeSettings(
            imapServer, request.ImapPort, request.UseSsl, request.EmailUsername.Trim(), request.Password, null,
            smtpServer, smtpPort, request.SmtpUseSsl ?? smtpPort == 465);
    }

    /// <summary>Nothing is saved for a mailbox that does not work: the first failed step is the message and every failure is listed in <c>problems</c>.</summary>
    public static async Task EnsureWorksAsync(IMailboxProbe probe, CoordinatorEmailConfigRequest request, CancellationToken cancellationToken)
    {
        var steps = await probe.ProbeAsync(SettingsOf(request), cancellationToken);
        var failed = steps.Where(step => step.State == "failed").ToList();
        if (failed.Count == 0) return;
        throw new AuthException("mailbox_test_failed", failed[0].Message ?? "The mailbox did not work.", 400)
        {
            Problems = failed.Select(step => $"{step.Message} {step.Hint}".Trim()).ToList()
        };
    }
}
