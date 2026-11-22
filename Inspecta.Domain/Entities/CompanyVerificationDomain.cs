namespace Inspecta.Domain.Entities;

/// <summary>
/// An address a company's certificates point to: the QR code of a certificate opens <c>https://{Host}/verify/{code}</c>. A company may have
/// several (the old ones stay so QR codes already printed keep working) and at most one is the primary, the one new certificates use.
/// <see cref="Host"/> is lower-case and unique across all companies. <see cref="ManagerId"/> is the company id, like every tenant table.
/// </summary>
public sealed class CompanyVerificationDomain
{
    public const int MaxHostLength = 253;

    private CompanyVerificationDomain() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The caller has already checked the host; it is stored lower-case.</summary>
    public static CompanyVerificationDomain Create(Guid managerId, string host, bool isPrimary, DateTimeOffset? now = null) => new()
    {
        Id = Guid.NewGuid(),
        ManagerId = managerId,
        Host = host.Trim().ToLowerInvariant(),
        IsPrimary = isPrimary,
        CreatedAt = now ?? DateTimeOffset.UtcNow
    };

    public void MakePrimary() => IsPrimary = true;

    public void Demote() => IsPrimary = false;
}
