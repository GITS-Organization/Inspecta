using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

/// <summary>Checks and decodes the files a coordinator adds to a quotation or certificates email.</summary>
public static class ExtraEmailAttachments
{
    public const int MaxFiles = 5;
    public const long MaxTotalBytes = 15 * 1024 * 1024;
    private const int MaxNameLength = 150;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip"
    };

    /// <summary>The files ready to attach; throws <see cref="ArgumentException"/> (a 400) naming what is wrong.</summary>
    public static IReadOnlyList<EmailAttachment> Decode(IReadOnlyList<EmailAttachmentUpload>? uploads)
    {
        if (uploads is not { Count: > 0 }) return [];
        if (uploads.Count > MaxFiles) throw new ArgumentException($"At most {MaxFiles} extra files can be attached.");

        var files = new List<EmailAttachment>(uploads.Count);
        long total = 0;
        foreach (var upload in uploads)
        {
            var name = CleanName(upload.FileName);
            if (!ContentTypes.TryGetValue(Path.GetExtension(name), out var contentType))
                throw new ArgumentException($"'{name}' cannot be attached: this type of file is not allowed.");

            byte[] content;
            try { content = Convert.FromBase64String(upload.ContentBase64 ?? string.Empty); }
            catch (FormatException) { throw new ArgumentException($"'{name}' is not valid base64."); }
            if (content.Length == 0) throw new ArgumentException($"'{name}' is empty.");

            total += content.Length;
            if (total > MaxTotalBytes)
                throw new ArgumentException($"The extra files are larger than {MaxTotalBytes / (1024 * 1024)} MB together.");
            if (!MatchesType(Path.GetExtension(name), content))
                throw new ArgumentException($"'{name}' is not what its extension says.");

            files.Add(new EmailAttachment(name, content, contentType));
        }

        return files;
    }

    private static string CleanName(string? fileName)
    {
        var name = string.Concat((fileName ?? string.Empty).Where(character => !char.IsControl(character))).Trim();
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..].Trim();
        if (name.Length == 0 || name.Length > MaxNameLength || name.StartsWith('.'))
            throw new ArgumentException("Every extra file needs a name with an extension, at most 150 characters.");
        return name;
    }

    // The first bytes of the file must be those of its type, so a renamed program is not sent.
    private static bool MatchesType(string extension, byte[] content) => extension.ToLowerInvariant() switch
    {
        ".pdf" => StartsWith(content, "%PDF"u8),
        ".png" => StartsWith(content, [0x89, 0x50, 0x4E, 0x47]),
        ".jpg" or ".jpeg" => StartsWith(content, [0xFF, 0xD8, 0xFF]),
        ".gif" => StartsWith(content, "GIF8"u8),
        ".zip" or ".docx" or ".xlsx" or ".pptx" => StartsWith(content, [0x50, 0x4B]),
        ".doc" or ".xls" or ".ppt" => StartsWith(content, [0xD0, 0xCF, 0x11, 0xE0]),
        _ => true
    };

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> prefix) => content.AsSpan().StartsWith(prefix);
}
