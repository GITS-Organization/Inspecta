namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Attachment metadata from the source email.</summary>
public sealed record QuotationEmailAttachmentResponse(string FileName, string ContentType, long Size);
