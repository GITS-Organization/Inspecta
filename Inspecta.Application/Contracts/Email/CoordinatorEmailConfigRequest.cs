using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Email;

/// <summary>IMAP credentials and connection settings for a coordinator mailbox.</summary>
public sealed record CoordinatorEmailConfigRequest
{
    [Required, MaxLength(255)]
    public required string ImapServer { get; init; }

    [Range(1, 65535)]
    public required int ImapPort { get; init; }

    [Required, MaxLength(320)]
    public required string EmailUsername { get; init; }

    [Required, MaxLength(256)]
    public required string Password { get; init; }

    public bool UseSsl { get; init; } = true;

    /// <summary>Outgoing mail server used to send quotations. Defaults to the IMAP server with imap changed to smtp.</summary>
    [MaxLength(255)]
    public string? SmtpServer { get; init; }

    /// <summary>Defaults to 587.</summary>
    [Range(1, 65535)]
    public int? SmtpPort { get; init; }

    /// <summary>True for implicit TLS (usually port 465), false for STARTTLS. Defaults to true only for port 465.</summary>
    public bool? SmtpUseSsl { get; init; }

    /// <summary>Import mail that arrived after this date. A new mailbox starts from 1 November 2026 when it is left out; changing it makes the import start again from the new date.</summary>
    public DateTimeOffset? SyncFrom { get; init; }

    /// <summary>
    /// True to try the mailbox (reach the server, sign in, read the inbox, send) before saving it: nothing is saved, and for a new coordinator no account is created,
    /// when it does not work (<c>400 mailbox_test_failed</c>, the failed steps in <c>problems</c>). A mailbox that works is saved already marked as checked. Defaults to false.
    /// </summary>
    public bool Verify { get; init; }
}

/// <summary>The mailbox settings of a coordinator without the password.</summary>
public sealed record CoordinatorEmailConfigResponse(
    bool Configured,
    string? ImapServer,
    int? ImapPort,
    string? EmailUsername,
    bool? UseSsl,
    string? SmtpServer,
    int? SmtpPort,
    bool? SmtpUseSsl,
    DateTimeOffset SyncFrom,
    DateTimeOffset? UpdatedAt,
    /// <summary><c>password</c> or <c>microsoft_oauth</c>.</summary>
    string AuthMode = "password",
    /// <summary><c>connected</c>, or <c>needs_reconnect</c> when Microsoft no longer accepts the saved sign-in.</summary>
    string ConnectionStatus = "connected",
    string? LastAuthError = null,
    DateTimeOffset? LastCheckedAt = null,
    DateTimeOffset? LastEmailAt = null);

/// <summary>Chooses the date mail is imported from.</summary>
public sealed record ChangeSyncFromRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public DateTimeOffset SyncFrom { get; init; }
}

/// <summary>One thing tried when a mailbox is tested: <c>reach</c>, <c>sign_in</c>, <c>read</c> or <c>send</c>, and how it went (<c>ok</c>, <c>failed</c> or <c>skipped</c> after an earlier failure).</summary>
public sealed record MailboxCheckStep(string Step, string State, string? Message = null, string? Hint = null);

/// <summary>The four steps of a mailbox test, in order. <c>Ok</c> only when every step worked.</summary>
public sealed record MailboxCheckResponse(bool Ok, IReadOnlyList<MailboxCheckStep> Steps);
