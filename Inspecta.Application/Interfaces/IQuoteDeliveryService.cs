using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>What the coordinator may change before the quotation email goes out; anything left out keeps the suggested value.</summary>
public sealed record QuoteEmailDraft(
    string? To = null,
    IReadOnlyList<string>? Cc = null,
    string? Subject = null,
    string? Body = null);

/// <summary>The quotation email as it would go out, and what stops it.</summary>
public sealed record QuoteEmailPlan(
    bool EmailEnabled,
    string? ToAddress,
    string? ToName,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    string AttachmentName,
    string? FromAddress,
    string? FromName,
    bool RepliesToOriginalEmail,
    IReadOnlyList<string> Blockers);

/// <summary>Generates the quotation PDF and emails it to whoever sent the original request.</summary>
public interface IQuoteDeliveryService
{
    /// <summary>
    /// Sends the quotation PDF as a reply to the email the request came from. Returns <c>null</c> when the request has
    /// no source email (a manual request), in which case nothing is sent. The caller changes the quote and request
    /// state only after this returns, so a failed delivery leaves them untouched.
    /// </summary>
    /// <exception cref="Exceptions.EmailDeliveryException">The mail server rejected or could not receive the message.</exception>
    Task<OutboundEmail?> DeliverAsync(
        InspectionRequest request,
        Quote quote,
        OutboundEmailKind kind,
        QuoteEmailDraft? draft = null,
        CancellationToken cancellationToken = default);

    /// <summary>The email that would go out, with the coordinator's edits applied. Sends nothing and changes nothing.</summary>
    Task<QuoteEmailPlan> PlanAsync(
        InspectionRequest request,
        Quote quote,
        OutboundEmailKind kind,
        QuoteEmailDraft? draft,
        CancellationToken cancellationToken = default);
}
