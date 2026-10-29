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

    /// <summary>Optional outgoing mail server. When empty it is derived from the IMAP server (imap.x becomes smtp.x).</summary>
    public string? SmtpServer { get; private set; }
    public int? SmtpPort { get; private set; }
    public bool? SmtpUseSsl { get; private set; }
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
        DateTimeOffset? updatedAt = null,
        string? smtpServer = null,
        int? smtpPort = null,
        bool? smtpUseSsl = null)
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
        SmtpServer = NormalizeSmtpServer(smtpServer);
        SmtpPort = ValidateSmtpPort(smtpPort);
        SmtpUseSsl = smtpUseSsl;
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
        DateTimeOffset? updatedAt = null,
        string? smtpServer = null,
        int? smtpPort = null,
        bool? smtpUseSsl = null)
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
            SmtpServer = NormalizeSmtpServer(smtpServer),
            SmtpPort = ValidateSmtpPort(smtpPort),
            SmtpUseSsl = smtpUseSsl,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            SyncFromUtc = updatedAt ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>The outgoing mail server to use: the configured one, or one derived from the IMAP server.</summary>
    public SmtpEndpoint ResolveSmtp()
    {
        var server = SmtpServer ?? DeriveSmtpServer(ImapServer);
        var port = SmtpPort ?? 587;
        return new SmtpEndpoint(server, port, SmtpUseSsl ?? port == 465);
    }

    /// <summary>imap.gmail.com becomes smtp.gmail.com and imap-mail.outlook.com becomes smtp-mail.outlook.com.</summary>
    public static string DeriveSmtpServer(string imapServer)
    {
        var host = imapServer.Trim();
        return host.StartsWith("imap", StringComparison.OrdinalIgnoreCase) ? "smtp" + host[4..] : host;
    }

    private static string? NormalizeSmtpServer(string? server)
    {
        var value = string.IsNullOrWhiteSpace(server) ? null : server.Trim();
        if (value is { Length: > 255 }) throw new ArgumentException("SMTP server is too long.", nameof(server));
        return value;
    }

    private static int? ValidateSmtpPort(int? port) =>
        port is < 1 or > 65535 ? throw new ArgumentOutOfRangeException(nameof(port)) : port;
}

/// <summary>Where and how to connect to the outgoing mail server.</summary>
public sealed record SmtpEndpoint(string Server, int Port, bool UseSsl);
