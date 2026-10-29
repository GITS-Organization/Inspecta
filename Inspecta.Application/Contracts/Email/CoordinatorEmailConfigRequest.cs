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
}
