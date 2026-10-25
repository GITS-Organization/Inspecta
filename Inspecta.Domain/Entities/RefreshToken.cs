namespace Inspecta.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public AppUser? User { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt is null && !IsExpired;

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt, Guid? familyId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Token expiry must be in the future.", nameof(expiresAt));

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            FamilyId = familyId ?? Guid.NewGuid()
        };
    }

    public void Revoke(DateTimeOffset? revokedAt = null) =>
        RevokedAt ??= revokedAt ?? DateTimeOffset.UtcNow;

    public void MarkReplacedBy(string replacementTokenHash)
    {
        if (string.IsNullOrWhiteSpace(replacementTokenHash))
            throw new ArgumentException("Replacement token hash is required.", nameof(replacementTokenHash));
        if (RevokedAt is null)
            throw new InvalidOperationException("A refresh token must be revoked before replacement is recorded.");
        ReplacedByTokenHash = replacementTokenHash;
    }
}
