namespace Inspecta.Application.Contracts.Mail;

/// <summary>A file the coordinator adds to an email, sent inside the request that sends the email.</summary>
/// <param name="FileName">The name the client sees, with its extension (for example <c>terms.pdf</c>).</param>
/// <param name="ContentBase64">The file, base64 encoded.</param>
public sealed record EmailAttachmentUpload(string FileName, string ContentBase64);
