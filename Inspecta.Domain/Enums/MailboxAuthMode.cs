namespace Inspecta.Domain.Enums;

/// <summary>How the app signs in to a coordinator's mailbox.</summary>
public enum MailboxAuthMode
{
    /// <summary>A stored (encrypted) mailbox password, as for Gmail with an app password.</summary>
    Password = 0,

    /// <summary>A Microsoft sign-in: an encrypted refresh token gives access tokens for IMAP and SMTP.</summary>
    MicrosoftOAuth = 1
}

/// <summary>Whether the app can currently use the mailbox's saved sign-in.</summary>
public enum MailboxConnectionStatus
{
    Connected = 0,

    /// <summary>Microsoft no longer accepts the saved sign-in: the coordinator must connect again.</summary>
    NeedsReconnect = 1
}
