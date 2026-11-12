using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Interfaces;

public interface IRequestTimelineService
{
    /// <summary>The timeline of a request the actor can see, or null when it does not exist or is not visible to them.</summary>
    Task<RequestTimelineResponse?> GetAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
}
