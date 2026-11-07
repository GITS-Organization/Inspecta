namespace Inspecta.Application.Interfaces;

/// <summary>Where to send the coordinator browser to sign in to Microsoft.</summary>
public sealed record StartMicrosoftConnectionResponse(string AuthorizationUrl);

/// <summary>What the coordinator browser is told after Microsoft sent it back; <see cref="Code"/> names what went wrong.</summary>
public sealed record MicrosoftConnectionResult(bool Connected, string Message, string? MailboxAddress, string? Code = null);

public interface IMicrosoftMailboxConnectionService
{
    /// <summary>The Microsoft sign-in address for this coordinator (a signed-in coordinator of an active team).</summary>
    /// <exception cref="Exceptions.AuthException"><c>microsoft_not_configured</c> (503) when the server has no Microsoft app settings.</exception>
    Task<StartMicrosoftConnectionResponse> StartAsync(Guid coordinatorId, CancellationToken cancellationToken = default);

    /// <summary>Finishes the sign-in Microsoft redirected back from. Never throws for a bad request: the result says what failed.</summary>
    Task<MicrosoftConnectionResult> CompleteAsync(string? state, string? code, string? error, CancellationToken cancellationToken = default);
}
