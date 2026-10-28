namespace Inspecta.Domain.Entities;

public sealed class CoordinatorEmailConfig
{
    private CoordinatorEmailConfig() { }

    public Guid CoordinatorId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ImapServer { get; private set; } = string.Empty;
    public int ImapPort { get; private set; }
    public string EmailUsername { get; private set; } = string.Empty;
    public string EncryptedPassword { get; private set; } = string.Empty;
    public bool UseSsl { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string imapServer,
        int imapPort,
        string emailUsername,
        string encryptedPassword,
        bool useSsl,
        DateTimeOffset? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(imapServer))
            throw new ArgumentException("IMAP server is required.", nameof(imapServer));
        if (imapPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(imapPort));
        if (string.IsNullOrWhiteSpace(emailUsername))
            throw new ArgumentException("Email username is required.", nameof(emailUsername));
        if (string.IsNullOrWhiteSpace(encryptedPassword))
            throw new ArgumentException("Encrypted password is required.", nameof(encryptedPassword));

        ImapServer = imapServer.Trim();
        ImapPort = imapPort;
        EmailUsername = emailUsername.Trim();
        EncryptedPassword = encryptedPassword;
        UseSsl = useSsl;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public static CoordinatorEmailConfig Create(
        Guid coordinatorId,
        Guid tenantId,
        string imapServer,
        int imapPort,
        string emailUsername,
        string encryptedPassword,
        bool useSsl,
        DateTimeOffset? updatedAt = null)
    {
        if (coordinatorId == Guid.Empty || tenantId == Guid.Empty)
            throw new ArgumentException("Coordinator and tenant ids are required.");
        if (string.IsNullOrWhiteSpace(imapServer))
            throw new ArgumentException("IMAP server is required.", nameof(imapServer));
        if (imapPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(imapPort));
        if (string.IsNullOrWhiteSpace(emailUsername))
            throw new ArgumentException("Email username is required.", nameof(emailUsername));
        if (string.IsNullOrWhiteSpace(encryptedPassword))
            throw new ArgumentException("Encrypted password is required.", nameof(encryptedPassword));

        return new CoordinatorEmailConfig
        {
            CoordinatorId = coordinatorId,
            TenantId = tenantId,
            ImapServer = imapServer.Trim(),
            ImapPort = imapPort,
            EmailUsername = emailUsername.Trim(),
            EncryptedPassword = encryptedPassword,
            UseSsl = useSsl,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };
    }
}
