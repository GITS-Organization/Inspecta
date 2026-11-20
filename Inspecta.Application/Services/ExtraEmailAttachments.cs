using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

/// <summary>Checks and decodes the files a coordinator adds to a quotation or certificates email.</summary>
public static class ExtraEmailAttachments
{
    public const int MaxFiles = 5;
    public const long MaxTotalBytes = 15 * 1024 * 1024;

    /// <summary>The files ready to attach; throws <see cref="ArgumentException"/> (a 400) naming what is wrong.</summary>
    public static IReadOnlyList<EmailAttachment> Decode(IReadOnlyList<EmailAttachmentUpload>? uploads)
    {
        if (uploads is not { Count: > 0 }) return [];
        if (uploads.Count > MaxFiles) throw new ArgumentException($"At most {MaxFiles} extra files can be attached.");

        var files = new List<EmailAttachment>(uploads.Count);
        long total = 0;
        foreach (var upload in uploads)
        {
            var name = UploadedFileRules.CleanName(upload.FileName)
                ?? throw new ArgumentException("Every extra file needs a name with an extension, at most 150 characters.");
            if (!UploadedFileRules.TryGetContentType(Path.GetExtension(name), allowZip: true, out var contentType))
                throw new ArgumentException($"'{name}' cannot be attached: this type of file is not allowed.");

            byte[] content;
            try { content = Convert.FromBase64String(upload.ContentBase64 ?? string.Empty); }
            catch (FormatException) { throw new ArgumentException($"'{name}' is not valid base64."); }
            if (content.Length == 0) throw new ArgumentException($"'{name}' is empty.");

            total += content.Length;
            if (total > MaxTotalBytes)
                throw new ArgumentException($"The extra files are larger than {MaxTotalBytes / (1024 * 1024)} MB together.");
            if (!UploadedFileRules.MatchesType(Path.GetExtension(name), content))
                throw new ArgumentException($"'{name}' is not what its extension says.");

            files.Add(new EmailAttachment(name, content, contentType));
        }

        return files;
    }
}
