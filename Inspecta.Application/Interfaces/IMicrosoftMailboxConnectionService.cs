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

    /// <summary>
    /// The company admin starts the Microsoft sign-in for one coordinator of the company. Whoever signs in to Microsoft on the page it opens connects their mailbox
    /// to that coordinator (the mailbox must be on the company's domain). <c>coordinator_not_found</c> (404) when the person is not an active coordinator of that company.
    /// </summary>
    Task<StartMicrosoftConnectionResponse> StartForAsync(Guid managerId, Guid coordinatorId, CancellationToken cancellationToken = default);

    /// <summary>Finishes the sign-in Microsoft redirected back from. Never throws for a bad request: the result says what failed.</summary>
    Task<MicrosoftConnectionResult> CompleteAsync(string? state, string? code, string? error, string? errorDescription = null, CancellationToken cancellationToken = default);
}
