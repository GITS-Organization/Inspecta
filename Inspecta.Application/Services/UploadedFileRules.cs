namespace Inspecta.Application.Services;

/// <summary>
/// The checks shared by every file a person uploads inside a JSON body (extra email files, job reports): the cleaned name, the
/// type taken from the extension (never from the client) and the first bytes of the file matching that type.
/// </summary>
internal static class UploadedFileRules
{
    public const int MaxNameLength = 150;

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

    /// <summary>The content type of an allowed extension (with the dot), or false. A ZIP is allowed only when <paramref name="allowZip"/> says so.</summary>
    public static bool TryGetContentType(string extension, bool allowZip, out string contentType)
    {
        if (!allowZip && extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            contentType = string.Empty;
            return false;
        }

        return ContentTypes.TryGetValue(extension, out contentType!);
    }

    /// <summary>The file name without any path or control character, or null when it is empty, hidden (starts with a dot) or too long.</summary>
    public static string? CleanName(string? fileName)
    {
        var name = string.Concat((fileName ?? string.Empty).Where(character => !char.IsControl(character))).Trim();
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..].Trim();
        return name.Length == 0 || name.Length > MaxNameLength || name.StartsWith('.') ? null : name;
    }

    // The first bytes of the file must be those of its type, so a renamed program is not accepted.
    public static bool MatchesType(string extension, byte[] content) => extension.ToLowerInvariant() switch
    {
        ".pdf" => StartsWith(content, "%PDF"u8),
        ".png" => StartsWith(content, [0x89, 0x50, 0x4E, 0x47]),
        ".jpg" or ".jpeg" => StartsWith(content, [0xFF, 0xD8, 0xFF]),
        ".gif" => StartsWith(content, "GIF8"u8),
        ".zip" or ".docx" or ".xlsx" or ".pptx" => StartsWith(content, [0x50, 0x4B]),
        ".doc" or ".xls" or ".ppt" => StartsWith(content, [0xD0, 0xCF, 0x11, 0xE0]),
        _ => true
    };

    /// <summary>A Windows program (starts with MZ) or a Linux one (starts with 0x7F ELF), whatever the extension says.</summary>
    public static bool LooksLikeProgram(byte[] content) =>
        StartsWith(content, "MZ"u8) || StartsWith(content, [0x7F, 0x45, 0x4C, 0x46]);

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> prefix) => content.AsSpan().StartsWith(prefix);
}
