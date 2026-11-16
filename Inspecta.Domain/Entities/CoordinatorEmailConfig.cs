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

    /// <summary>
    /// The mailboxes the background sync reads: every one except those waiting for the coordinator to connect Outlook again. A mailbox with a problem is
    /// read again every cycle, so a network blip or a temporary outage mends itself the next time the sync signs in.
    /// </summary>
    public static readonly System.Linq.Expressions.Expression<Func<CoordinatorEmailConfig, bool>> IsReadableBySync =
        config => config.ConnectionStatus != MailboxConnectionStatus.NeedsReconnect;

    /// <summary>How long a sync waits before writing the same news about a mailbox again (that it still works, or that it still fails the same way).</summary>
    private static readonly TimeSpan SyncRecordInterval = TimeSpan.FromMinutes(15);

    /// <summary>When the mailbox was last tried (a sync, a check or a test of the saved settings); <c>null</c> until then.</summary>
    public DateTimeOffset? LastCheckedAt { get; private set; }

    /// <summary>When the newest email imported from this mailbox arrived; <c>null</c> until one did.</summary>
    public DateTimeOffset? LastEmailAt { get; private set; }

    /// <summary>The step of the last try that did not work (<c>reach</c>, <c>sign_in</c>, <c>read</c> or <c>send</c>); <c>null</c> while the mailbox works.</summary>
    public string? LastFailedStep { get; private set; }

    /// <summary>A full test passed (reach, sign in, read, send): whatever was wrong is over.</summary>
    public void RecordSuccessfulCheck(DateTimeOffset at)
    {
        LastCheckedAt = at;
        ConnectionStatus = MailboxConnectionStatus.Connected;
        LastAuthError = null;
        LastFailedStep = null;
    }

    /// <summary>
    /// A check or test found something wrong at <paramref name="step"/>. It is a problem with its reason. Only Microsoft refusing the saved sign-in (the token
    /// refresh, handled elsewhere) means the coordinator must connect again, so a reconnect that is waiting is never replaced, and one refused IMAP sign-in
    /// is not turned into a reconnect.
    /// </summary>
    public void RecordFailedCheck(DateTimeOffset at, string reason, string step)
    {
        LastCheckedAt = at;
        if (ConnectionStatus == MailboxConnectionStatus.NeedsReconnect) return;
        ConnectionStatus = MailboxConnectionStatus.Problem;
        LastAuthError = Clip(reason);
        LastFailedStep = step;
    }

    /// <summary>
    /// A sync signed in and opened the inbox. That ends a problem with reaching, signing in or reading, but never a sending problem (a sync does not send).
    /// Gives <c>false</c> when there is nothing new to write: the mailbox was already fine and was written a short while ago.
    /// </summary>
    public bool RecordSyncSuccess(DateTimeOffset at)
    {
        var clears = ConnectionStatus == MailboxConnectionStatus.Problem && LastFailedStep != "send";
        if (!clears && LastCheckedAt is { } last && at - last < SyncRecordInterval) return false;
        LastCheckedAt = at;
        if (clears)
        {
            ConnectionStatus = MailboxConnectionStatus.Connected;
            LastAuthError = null;
            LastFailedStep = null;
        }
        return true;
    }

    /// <summary>A sync could not use the mailbox. Gives <c>false</c> when it is the same failure that was written a short while ago.</summary>
    public bool RecordSyncFailure(DateTimeOffset at, string reason, string step)
    {
        if (ConnectionStatus == MailboxConnectionStatus.NeedsReconnect) return false;
        // A sending problem is only ended by a check or a save that tries sending: a sync failing elsewhere must not overwrite it, or a later good sync would hide it.
        if (ConnectionStatus == MailboxConnectionStatus.Problem && LastFailedStep == "send")
        {
            if (LastCheckedAt is { } checkedAt && at - checkedAt < SyncRecordInterval) return false;
            LastCheckedAt = at;
            return true;
        }
        var text = Clip(reason);
        var same = ConnectionStatus == MailboxConnectionStatus.Problem && LastFailedStep == step && LastAuthError == text;
        if (same && LastCheckedAt is { } last && at - last < SyncRecordInterval) return false;
        LastCheckedAt = at;
        ConnectionStatus = MailboxConnectionStatus.Problem;
        LastAuthError = text;
        LastFailedStep = step;
        return true;
    }

    /// <summary>An email from this mailbox was imported; only a newer one moves the date, and never into the future (the date is the sender's header).</summary>
    public void RecordEmailReceived(DateTimeOffset at)
    {
        var now = DateTimeOffset.UtcNow;
        if (at > now) at = now;
        if (LastEmailAt is null || at > LastEmailAt) LastEmailAt = at;
    }

    private static string Clip(string? reason)
    {
        var text = reason?.Trim() ?? string.Empty;
        return text.Length > MaxAuthErrorLength ? text[..MaxAuthErrorLength] : text;
    }

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
        if (mailboxChanged)
        {
            ResetSync(UpdatedAt);
            LastEmailAt = null;
        }
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
        LastFailedStep = null;
        LastCheckedAt = null;
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
        LastFailedStep = null;
        LastCheckedAt = null;

        if (mailboxChanged) LastEmailAt = null;
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
