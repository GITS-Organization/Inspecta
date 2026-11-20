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

    /// <summary>
    /// A .docx, .xlsx or .pptx is a ZIP that holds a <c>[Content_Types].xml</c> entry; a ZIP of anything else renamed to one of them is refused.
    /// Other extensions are not affected.
    /// </summary>
    public static bool IsGenuineOfficeFile(string extension, byte[] content)
    {
        if (extension.ToLowerInvariant() is not (".docx" or ".xlsx" or ".pptx")) return true;
        try
        {
            using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(content), System.IO.Compression.ZipArchiveMode.Read);
            return archive.GetEntry("[Content_Types].xml") is not null;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// A Windows program (MZ header with the usual 0x90 0x00 after it, or a PE header where the header says) or a Linux one (0x7F ELF),
    /// whatever the extension says. A plain text that merely starts with the letters MZ (a project code, for example) is not a program.
    /// </summary>
    public static bool LooksLikeProgram(byte[] content) =>
        StartsWith(content, [0x4D, 0x5A, 0x90, 0x00]) || HasPortableExecutableHeader(content) || StartsWith(content, [0x7F, 0x45, 0x4C, 0x46]);

    private static bool HasPortableExecutableHeader(byte[] content)
    {
        if (!StartsWith(content, "MZ"u8) || content.Length < 64) return false;
        var offset = BitConverter.ToInt32(content, 60);
        return offset >= 0 && offset <= content.Length - 4 && content.AsSpan(offset, 4).SequenceEqual("PE\0\0"u8);
    }

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> prefix) => content.AsSpan().StartsWith(prefix);
}
