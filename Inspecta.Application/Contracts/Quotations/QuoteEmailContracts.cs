using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>
/// What the coordinator may change in the email that carries the quotation PDF. Every field is optional: what is left out keeps the
/// suggestion of <c>GET .../email</c>. The PDF itself cannot be changed here.
/// </summary>
public sealed record SendQuoteEmailRequest
{
    /// <summary>Who receives it. Defaults to the sender of the original email; needed when the request did not come from an email.</summary>
    [MaxLength(320)]
    public string? SentTo { get; init; }

    /// <summary>Copies, at most 10.</summary>
    public IReadOnlyList<string>? Cc { get; init; }

    [MaxLength(300)]
    public string? Subject { get; init; }

    /// <summary>The whole text of the email, greeting and sign-off included.</summary>
    [MaxLength(20000)]
    public string? Message { get; init; }
}

/// <summary>The email that would carry the quotation PDF to the client, ready to be edited and sent.</summary>
/// <param name="Action"><c>send</c> (an offer or a counter offer) or <c>accept</c>.</param>
/// <param name="EmailEnabled">False while emailing is switched off: sending then only changes the quotation.</param>
/// <param name="PdfUrl">The attached quotation, to open before sending.</param>
/// <param name="CanSend">Whether the Send button works; <c>blockers</c> says why not.</param>
public sealed record QuoteEmailPreviewResponse(
    string Action,
    bool EmailEnabled,
    string? To,
    string? ToName,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    string? From,
    string AttachmentName,
    string PdfUrl,
    bool RepliesToOriginalEmail,
    string? Notice,
    bool CanSend,
    IReadOnlyList<string> Blockers);
