using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>A received client email parsed into a quotation draft.</summary>
public sealed record QuotationDraftResponse(
    Guid Id,
    Guid CoordinatorId,
    string CoordinatorName,
    string? ClientName,
    string? Location,
    string? MachineDetails,
    long? AmountPiastres,
    DateTimeOffset? ValidUntil,
    string RawEmailBody,
    string? EmailHtmlBody,
    string? SenderEmail,
    string? SenderName,
    string? EmailSubject,
    IReadOnlyList<QuotationEmailAddressResponse> To,
    IReadOnlyList<QuotationEmailAddressResponse> Cc,
    IReadOnlyList<QuotationEmailAddressResponse> Bcc,
    IReadOnlyList<QuotationEmailAddressResponse> ReplyTo,
    IReadOnlyList<QuotationEmailAttachmentResponse> Attachments,
    string? EmailMessageId,
    string? InReplyTo,
    string? References,
    DateTimeOffset? EmailDate,
    QuotationDraftStatus Status,
    DateTimeOffset CreatedAt);
