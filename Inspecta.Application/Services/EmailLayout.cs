using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>Who signs the email.</summary>
public sealed record EmailSignatureInfo(string Name, string? JobTitle, string? Phone, string Email, EmailBranding Branding);

/// <summary>The email being answered, shown below the reply the way a mail program quotes it.</summary>
public sealed record QuotedEmail(string? From, DateTimeOffset? At, string? To, string? Cc, string? Subject, string? Text);

/// <summary>An email as it goes out: plain text, HTML and the images the HTML shows.</summary>
public sealed record LaidOutEmail(string Text, string Html, IReadOnlyList<EmailAttachment> InlineImages);

/// <summary>
/// The look of the emails Inspecta sends: the coordinator's text, then the signature (company logo, name, job title, phone, email,
/// address, website), then the email being answered. The coordinator edits only the text; the rest is added when the email goes out.
/// </summary>
public static class EmailLayout
{
    public const string LogoContentId = "inspecta-logo";
    private const int MaxQuotedLength = 3000;
    private const string Green = "#3FA14D";

    /// <summary>The job title printed under the name, from the person's role.</summary>
    public static string? JobTitleOf(UserRole role) => role switch
    {
        UserRole.Coordinator => "Business Coordinator",
        UserRole.TechnicalManager => "Technical Manager",
        _ => null
    };

    /// <param name="logoSource">
    /// What the logo's <c>src</c> is: <c>cid:...</c> in an email that carries the logo, or a web address for a preview on screen.
    /// </param>
    public static LaidOutEmail Build(string bodyText, EmailSignatureInfo? signature, QuotedEmail? original, string? logoSource = null)
    {
        var text = new StringBuilder(bodyText.TrimEnd());
        var html = new StringBuilder(CertificateDeliveryService.PlainTextToHtml(bodyText.TrimEnd()));
        var images = new List<EmailAttachment>();

        if (signature is not null)
        {
            var branding = signature.Branding;
            var hasLogo = branding.LogoPng is { Length: > 0 };
            var source = logoSource ?? "cid:" + LogoContentId;
            if (hasLogo && logoSource is null)
                images.Add(new EmailAttachment(LogoContentId, branding.LogoPng!, "image/png"));
            text.Append(Environment.NewLine).Append(Environment.NewLine).Append(SignatureText(signature));
            html.Append(SignatureHtml(signature, hasLogo ? source : null));
        }

        if (original is not null)
        {
            text.Append(Environment.NewLine).Append(Environment.NewLine).Append(QuotedText(original));
            html.Append(QuotedHtml(original));
        }

        return new LaidOutEmail(text.ToString() + Environment.NewLine, html.ToString(), images);
    }

    /// <summary>
    /// The email of a coordinator as it goes out. Without branding (not configured) the text stays as it is, with no signature and
    /// nothing quoted.
    /// </summary>
    public static LaidOutEmail ForCoordinator(
        string bodyText, AppUser? coordinator, string? fromAddress, RequestEmail? source, IEmailBranding? branding, string? logoSource = null)
    {
        if (branding is null || coordinator is null || string.IsNullOrWhiteSpace(fromAddress))
            return new LaidOutEmail(bodyText, CertificateDeliveryService.PlainTextToHtml(bodyText), []);
        var signature = new EmailSignatureInfo(coordinator.FullName, JobTitleOf(coordinator.Role), coordinator.Phone, fromAddress, branding.Get());
        return Build(bodyText, signature, Quote(source), logoSource);
    }

    /// <summary>The address the logo is served from for a preview on screen (a real email carries it inside).</summary>
    public const string PreviewLogoPath = "/api/v1/branding/logo.png";

    /// <summary>The company of the team signs the email; a team without a company profile uses the built-in one.</summary>
    public static IEmailBranding? BrandingFor(CompanyBranding? company, IEmailBranding? builtIn) =>
        company is null ? builtIn : new FixedBranding(new EmailBranding(company.Name, company.Address, company.Website, company.LogoPng));

    /// <summary>The logo of the team's company as a preview on screen loads it.</summary>
    public static string PreviewLogoFor(Guid? managerId, CompanyBranding? company) =>
        company?.LogoPng is { Length: > 0 } && managerId is Guid id ? $"/api/v1/branding/{id}/logo.png" : PreviewLogoPath;

    private sealed class FixedBranding(EmailBranding value) : IEmailBranding
    {
        public EmailBranding Get() => value;
    }

    /// <summary>The email a reply answers: sender, date, recipients, subject and the beginning of its text.</summary>
    public static QuotedEmail? Quote(RequestEmail? source)
    {
        if (source is null) return null;
        string Names(IEnumerable<EmailAddressInfo> addresses) =>
            string.Join("; ", addresses.Select(address => string.IsNullOrWhiteSpace(address.Name) ? address.Address : $"{address.Name} <{address.Address}>"));
        var from = string.IsNullOrWhiteSpace(source.FromAddress)
            ? source.FromName
            : string.IsNullOrWhiteSpace(source.FromName) ? source.FromAddress : $"{source.FromName} <{source.FromAddress}>";
        var body = !string.IsNullOrWhiteSpace(source.TextBody) ? source.TextBody : PlainFromHtml(source.HtmlBody);
        return new QuotedEmail(from, source.SentAt ?? source.ReceivedAt, Names(source.GetToAddresses()), Names(source.GetCcAddresses()), source.Subject, body);
    }

    // "Hager Samy" signs as "H.Samy".
    public static string ShortName(string fullName)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !word.Equals("Eng.", StringComparison.OrdinalIgnoreCase) && !word.Equals("Eng", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return words.Length < 2 ? string.Join(' ', words) : $"{char.ToUpperInvariant(words[0][0])}.{words[^1]}";
    }

    private static string SignatureText(EmailSignatureInfo signature)
    {
        var lines = new List<string> { "With Appreciation,", ShortName(signature.Name), string.Empty, signature.Name };
        if (!string.IsNullOrWhiteSpace(signature.JobTitle)) lines.Add(signature.JobTitle);
        if (!string.IsNullOrWhiteSpace(signature.Phone)) lines.Add("Tel: " + signature.Phone);
        lines.Add("Email: " + signature.Email);
        if (!string.IsNullOrWhiteSpace(signature.Branding.Address)) lines.Add(signature.Branding.Address);
        lines.Add(signature.Branding.CompanyName);
        if (!string.IsNullOrWhiteSpace(signature.Branding.Website)) lines.Add(WebAddress(signature.Branding.Website));
        return string.Join(Environment.NewLine, lines);
    }

    private static string SignatureHtml(EmailSignatureInfo signature, string? logoSource)
    {
        string Encode(string value) => WebUtility.HtmlEncode(value);
        var branding = signature.Branding;
        var html = new StringBuilder();
        html.Append("<div style=\"margin-top:22px;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.5;color:#222\">");
        html.Append($"<div style=\"color:{Green};font-weight:700;font-size:17px\">With Appreciation,</div>");
        html.Append($"<div style=\"color:{Green};font-weight:700;font-size:17px;margin-bottom:12px\">{Encode(ShortName(signature.Name))}</div>");
        html.Append($"<div style=\"font-family:Georgia,'Times New Roman',serif;font-size:23px;font-weight:700;color:#111\">{Encode(signature.Name)}</div>");
        if (!string.IsNullOrWhiteSpace(signature.JobTitle))
            html.Append($"<div style=\"font-family:Georgia,'Times New Roman',serif;font-size:16px;color:#2e7d32\">{Encode(signature.JobTitle)}</div>");
        html.Append("<div style=\"margin-top:10px\">");
        if (!string.IsNullOrWhiteSpace(signature.Phone))
            html.Append($"<div>&#128222; <a href=\"tel:{Encode(Regex.Replace(signature.Phone, @"[^\d+]", string.Empty))}\" style=\"color:#1565C0\">{Encode(signature.Phone)}</a></div>");
        html.Append($"<div>&#9993; <a href=\"mailto:{Encode(signature.Email)}\" style=\"color:#1565C0\">{Encode(signature.Email)}</a></div>");
        if (!string.IsNullOrWhiteSpace(branding.Address))
            html.Append($"<div>&#128205; <span style=\"color:#1565C0\">{Encode(branding.Address)}</span></div>");
        html.Append("</div>");
        if (logoSource is not null)
            html.Append($"<div style=\"margin-top:12px\"><img src=\"{Encode(logoSource)}\" alt=\"{Encode(branding.CompanyName)}\" width=\"230\" style=\"display:block;border:0\"></div>");
        if (!string.IsNullOrWhiteSpace(branding.Website))
            html.Append($"<div style=\"margin-top:6px\">&#127760; <a href=\"{Encode(WebAddress(branding.Website))}\" style=\"color:#1565C0\">{Encode(WebAddress(branding.Website))}</a></div>");
        html.Append("</div>");
        return html.ToString();
    }

    private static string QuotedText(QuotedEmail original)
    {
        var lines = new List<string> { "________________________________" };
        if (!string.IsNullOrWhiteSpace(original.From)) lines.Add("From: " + original.From);
        if (original.At is { } at) lines.Add("Date: " + Format(at));
        if (!string.IsNullOrWhiteSpace(original.To)) lines.Add("To: " + original.To);
        if (!string.IsNullOrWhiteSpace(original.Cc)) lines.Add("Cc: " + original.Cc);
        if (!string.IsNullOrWhiteSpace(original.Subject)) lines.Add("Subject: " + original.Subject);
        if (Shorten(original.Text) is { Length: > 0 } text)
        {
            lines.Add(string.Empty);
            lines.Add(text);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string QuotedHtml(QuotedEmail original)
    {
        string Encode(string value) => WebUtility.HtmlEncode(value);
        var html = new StringBuilder();
        html.Append("<div style=\"margin-top:20px;border-top:1px solid #b5b5b5;padding-top:10px;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.5;color:#222\">");
        void Line(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) html.Append($"<div><b>{label}:</b> {Encode(value)}</div>");
        }

        Line("From", original.From);
        Line("Date", original.At is { } at ? Format(at) : null);
        Line("To", original.To);
        Line("Cc", original.Cc);
        Line("Subject", original.Subject);
        if (Shorten(original.Text) is { Length: > 0 } text)
            html.Append($"<div style=\"margin-top:10px;color:#555;white-space:pre-wrap\">{Encode(text)}</div>");
        html.Append("</div>");
        return html.ToString();
    }

    // Tuesday, 27 October 2026 at 4:33 PM (Cairo time when the server knows it).
    private static string Format(DateTimeOffset at)
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            try
            {
                at = TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(id));
                break;
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }

        return at.ToString("dddd, d MMMM yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);
    }

    private static string Shorten(string? text)
    {
        var clean = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        clean = Regex.Replace(clean, @"\n{3,}", "\n\n");
        return clean.Length > MaxQuotedLength ? clean[..MaxQuotedLength].TrimEnd() + "..." : clean;
    }

    private static string PlainFromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var withBreaks = Regex.Replace(html, @"<(br|/p|/div|/tr|/li)[^>]*>", "\n", RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(withBreaks, @"<(script|style)[\s\S]*?</\1>", string.Empty, RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, "<[^>]+>", string.Empty);
        return Regex.Replace(WebUtility.HtmlDecode(stripped), @"[ \t]+", " ");
    }

    private static string WebAddress(string website) =>
        website.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? website.Trim() : "https://" + website.Trim();
}
