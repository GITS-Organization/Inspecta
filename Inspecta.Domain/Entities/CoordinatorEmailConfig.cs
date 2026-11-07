using Inspecta.Domain.Enums;

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

    /// <summary>The date mail is imported from when nobody chose one: 1 November 2026.</summary>
    public static readonly DateTimeOffset DefaultSyncFromUtc = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Only mail that arrived after this moment is imported; older mailbox content is ignored.</summary>
    public DateTimeOffset SyncFromUtc { get; private set; }

    /// <summary>UID validity of the mailbox folder the cursor belongs to; <c>null</c> until the first sync.</summary>
    public string? LastUidValidity { get; private set; }

    /// <summary>Highest mailbox UID already handled.</summary>
    public long LastUid { get; private set; }

    public const string MicrosoftImapServer = "outlook.office365.com";
    public const int MicrosoftImapPort = 993;
    public const string MicrosoftSmtpServer = "smtp.office365.com";
    public const int MicrosoftSmtpPort = 587;
    private const int MaxAuthErrorLength = 500;

    /// <summary>A stored password (default) or a Microsoft sign-in.</summary>
    public MailboxAuthMode AuthMode { get; private set; } = MailboxAuthMode.Password;

    /// <summary>The Microsoft refresh token, encrypted; only for <see cref="MailboxAuthMode.MicrosoftOAuth"/>.</summary>
    public string? EncryptedRefreshToken { get; private set; }

    /// <summary>The Microsoft directory (tenant) the mailbox belongs to.</summary>
    public string? MicrosoftTenantId { get; private set; }

    public MailboxConnectionStatus ConnectionStatus { get; private set; } = MailboxConnectionStatus.Connected;

    /// <summary>Why the saved sign-in stopped working, for the person who has to connect again.</summary>
    public string? LastAuthError { get; private set; }

    /// <summary>A mailbox the coordinator connected by signing in to Microsoft: Outlook's servers and no password.</summary>
    public static CoordinatorEmailConfig CreateMicrosoft(
        Guid coordinatorId,
        Guid tenantId,
        string mailboxAddress,
        string encryptedRefreshToken,
        string microsoftTenantId,
        DateTimeOffset? syncFromUtc = null)
    {
        if (coordinatorId == Guid.Empty || tenantId == Guid.Empty)
            throw new ArgumentException("Coordinator and tenant ids are required.");
        var now = DateTimeOffset.UtcNow;
        var config = new CoordinatorEmailConfig
        {
            CoordinatorId = coordinatorId,
            TenantId = tenantId,
            UpdatedAt = now,
            SyncFromUtc = syncFromUtc ?? now
        };
        config.ApplyMicrosoft(mailboxAddress, encryptedRefreshToken, microsoftTenantId);
        return config;
    }

    /// <summary>Connects (or reconnects) the mailbox through Microsoft; a different mailbox starts the import again from now.</summary>
    public void ConnectMicrosoft(string mailboxAddress, string encryptedRefreshToken, string microsoftTenantId)
    {
        var mailboxChanged = !string.Equals(EmailUsername, mailboxAddress?.Trim(), StringComparison.OrdinalIgnoreCase);
        ApplyMicrosoft(mailboxAddress, encryptedRefreshToken, microsoftTenantId);
        UpdatedAt = DateTimeOffset.UtcNow;
        if (mailboxChanged) ResetSync(UpdatedAt);
    }

    /// <summary>Microsoft handed out a new refresh token while renewing the access token.</summary>
    public void RotateRefreshToken(string encryptedRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(encryptedRefreshToken))
            throw new ArgumentException("Encrypted refresh token is required.", nameof(encryptedRefreshToken));
        EncryptedRefreshToken = encryptedRefreshToken;
    }

    /// <summary>Microsoft refused the saved sign-in; the sync skips the mailbox until the coordinator connects again.</summary>
    public void MarkNeedsReconnect(string reason)
    {
        ConnectionStatus = MailboxConnectionStatus.NeedsReconnect;
        var text = reason?.Trim() ?? string.Empty;
        LastAuthError = text.Length > MaxAuthErrorLength ? text[..MaxAuthErrorLength] : text;
    }

    private void ApplyMicrosoft(string mailboxAddress, string encryptedRefreshToken, string microsoftTenantId)
    {
        if (string.IsNullOrWhiteSpace(mailboxAddress))
            throw new ArgumentException("Mailbox address is required.", nameof(mailboxAddress));
        if (string.IsNullOrWhiteSpace(encryptedRefreshToken))
            throw new ArgumentException("Encrypted refresh token is required.", nameof(encryptedRefreshToken));
        if (string.IsNullOrWhiteSpace(microsoftTenantId))
            throw new ArgumentException("Microsoft tenant id is required.", nameof(microsoftTenantId));

        AuthMode = MailboxAuthMode.MicrosoftOAuth;
        ImapServer = MicrosoftImapServer;
        ImapPort = MicrosoftImapPort;
        UseSsl = true;
        SmtpServer = MicrosoftSmtpServer;
        SmtpPort = MicrosoftSmtpPort;
        SmtpUseSsl = false;
        EmailUsername = mailboxAddress.Trim();
        EncryptedPassword = string.Empty;
        EncryptedRefreshToken = encryptedRefreshToken;
        MicrosoftTenantId = microsoftTenantId.Trim();
        ConnectionStatus = MailboxConnectionStatus.Connected;
        LastAuthError = null;
    }

    public void Update(
        string imapServer,
        int imapPort,
        string emailUsername,
        string encryptedPassword,
        bool useSsl,
        DateTimeOffset? updatedAt = null,
        string? smtpServer = null,
        int? smtpPort = null,
        bool? smtpUseSsl = null,
        DateTimeOffset? syncFromUtc = null)
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

        // Setting a password means the mailbox no longer signs in through Microsoft.
        AuthMode = MailboxAuthMode.Password;
        EncryptedRefreshToken = null;
        MicrosoftTenantId = null;
        ConnectionStatus = MailboxConnectionStatus.Connected;
        LastAuthError = null;

        // A different mailbox has its own UIDs: start again from now instead of importing its history.
        // A date the user chose wins, and moves the start of the import by itself.
        if (syncFromUtc is { } chosen && chosen != SyncFromUtc) ResetSync(chosen);
        else if (mailboxChanged) ResetSync(UpdatedAt);
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
        bool? smtpUseSsl = null,
        DateTimeOffset? syncFromUtc = null)
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
            SyncFromUtc = syncFromUtc ?? updatedAt ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>The earliest date a person may choose to import mail from.</summary>
    public static readonly DateTimeOffset EarliestSyncFromUtc = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Starts the import again from a date the user chose: only mail that arrived after it is fetched.</summary>
    public void ChangeSyncFrom(DateTimeOffset from)
    {
        if (from < EarliestSyncFromUtc || from > DateTimeOffset.UtcNow.AddDays(30))
            throw new ArgumentOutOfRangeException(nameof(from), "Choose a date from 2020 up to 30 days ahead.");
        if (from == SyncFromUtc) return;
        ResetSync(from);
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
