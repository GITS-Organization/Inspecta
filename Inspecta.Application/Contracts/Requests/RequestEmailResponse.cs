namespace Inspecta.Application.Contracts.Requests;

/// <summary>The original email an inspection request was created from.</summary>
/// <remarks>
/// <c>HtmlBody</c> is untrusted content from the sender. Clients must sanitize it or ignore it and render <c>TextBody</c>.
/// Attachment content is not stored; only name, type and size are kept.
/// </remarks>
public sealed record RequestEmailResponse(
    Guid RequestId,
    string? FromAddress,
    string? FromName,
    string? Subject,
    IReadOnlyList<RequestEmailAddressResponse> To,
    IReadOnlyList<RequestEmailAddressResponse> Cc,
    IReadOnlyList<RequestEmailAddressResponse> Bcc,
    IReadOnlyList<RequestEmailAddressResponse> ReplyTo,
    string? MessageId,
    string? InReplyTo,
    string? References,
    DateTimeOffset? SentAt,
    DateTimeOffset ReceivedAt,
    string? TextBody,
    string? HtmlBody,
    IReadOnlyList<RequestEmailAttachmentResponse> Attachments);

/// <summary>An email address with an optional display name.</summary>
public sealed record RequestEmailAddressResponse(string Address, string? Name);

/// <summary>Metadata for an attachment on the original email.</summary>
public sealed record RequestEmailAttachmentResponse(string FileName, string ContentType, long Size);
