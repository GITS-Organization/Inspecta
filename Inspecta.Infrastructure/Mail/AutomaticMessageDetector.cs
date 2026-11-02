using MimeKit;

namespace Inspecta.Infrastructure.Mail;

/// <summary>Recognises the messages that are never a person's answer: out-of-office replies, bounces (delivery failures) and bulk mail.</summary>
public static class AutomaticMessageDetector
{
    public static bool IsAutomatic(MimeMessage message)
    {
        var autoSubmitted = message.Headers["Auto-Submitted"];
        if (!string.IsNullOrWhiteSpace(autoSubmitted) && !autoSubmitted.Trim().Equals("no", StringComparison.OrdinalIgnoreCase))
            return true;

        var precedence = message.Headers["Precedence"];
        if (precedence is not null &&
            (precedence.Contains("auto_reply", StringComparison.OrdinalIgnoreCase) ||
             precedence.Contains("bulk", StringComparison.OrdinalIgnoreCase) ||
             precedence.Contains("junk", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (message.Headers["X-Autoreply"] is not null || message.Headers["X-Autorespond"] is not null)
            return true;
        var suppress = message.Headers["X-Auto-Response-Suppress"];
        if (suppress is not null &&
            (suppress.Contains("OOF", StringComparison.OrdinalIgnoreCase) || suppress.Contains("AutoReply", StringComparison.OrdinalIgnoreCase)))
            return true;

        // A bounce: a delivery report, with no return path or from the mail system itself.
        if (message.Headers["Return-Path"]?.Trim() == "<>") return true;
        if (message.Body?.ContentType.IsMimeType("multipart", "report") == true) return true;
        var sender = message.From.Mailboxes.FirstOrDefault()?.Address;
        var local = sender is null ? null : sender[..Math.Max(sender.IndexOf('@'), 0)];
        return local is not null &&
               (local.Equals("mailer-daemon", StringComparison.OrdinalIgnoreCase) || local.Equals("postmaster", StringComparison.OrdinalIgnoreCase));
    }
}
