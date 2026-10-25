namespace Inspecta.Domain.Entities;

public sealed record ItemAttachment(string FileName, string StorageKey, string? ContentType, long SizeBytes)
{
    public static ItemAttachment Create(string fileName, string storageKey, string? contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Attachment file name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Attachment storage key is required.", nameof(storageKey));
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        return new ItemAttachment(fileName.Trim(), storageKey.Trim(), string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim(), sizeBytes);
    }
}
