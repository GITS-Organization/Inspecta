namespace Inspecta.Application.Services;

/// <summary>Settings of the Microsoft (Entra) app the coordinators sign in through; read from the <c>MicrosoftOAuth</c> section.</summary>
public sealed class MicrosoftOAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>The public address Microsoft sends the browser back to: <c>.../api/v1/email-connection/microsoft/callback</c>.</summary>
    public string? RedirectUri { get; set; }

    /// <summary><c>common</c> accepts work and personal Microsoft accounts.</summary>
    public string Authority { get; set; } = "https://login.microsoftonline.com/common";

    /// <summary>Lets a mailbox on any domain connect, for a test tenant. Off by default: the mailbox must be on the company domain.</summary>
    public bool AllowAnyDomain { get; set; }
}
