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

    /// <summary>Only mail that arrived after this moment is imported; older mailbox content is ignored.</summary>
    public DateTimeOffset SyncFromUtc { get; private set; }

    /// <summary>UID validity of the mailbox folder the cursor belongs to; <c>null</c> until the first sync.</summary>
    public string? LastUidValidity { get; private set; }

    /// <summary>Highest mailbox UID already handled.</summary>
    public long LastUid { get; private set; }

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

        var mailboxChanged =
            !string.Equals(ImapServer, imapServer.Trim(), StringComparison.OrdinalIgnoreCase) ||
            ImapPort != imapPort ||
            !string.Equals(EmailUsername, emailUsername.Trim(), StringComparison.OrdinalIgnoreCase);

        ImapServer = imapServer.Trim();
        ImapPort = imapPort;
        EmailUsername = emailUsername.Trim();
        EncryptedPassword = encryptedPassword;
        UseSsl = useSsl;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;

        // A different mailbox has its own UIDs: start again from now instead of importing its history.
        if (mailboxChanged) ResetSync(UpdatedAt);
    }

    public void ResetSync(DateTimeOffset from)
    {
        SyncFromUtc = from;
        LastUidValidity = null;
        LastUid = 0;
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
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            SyncFromUtc = updatedAt ?? DateTimeOffset.UtcNow
        };
    }
}
