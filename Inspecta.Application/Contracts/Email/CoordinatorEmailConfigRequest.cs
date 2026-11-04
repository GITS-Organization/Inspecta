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
    DateTimeOffset? UpdatedAt);

/// <summary>Chooses the date mail is imported from.</summary>
public sealed record ChangeSyncFromRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public DateTimeOffset SyncFrom { get; init; }
}
