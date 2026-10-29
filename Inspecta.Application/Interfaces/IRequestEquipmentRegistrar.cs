using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IRequestEquipmentRegistrar
{
    /// <summary>
    /// Registers every machine of the request that is not matched to a record yet, under the request client, and links
    /// the request item to it. A request without a client gets one: the team client with the same name, or a new one.
    /// Nothing is saved here; the caller saves the request and the staged records together.
    /// </summary>
    Task RegisterAsync(InspectionRequest request, CancellationToken cancellationToken = default);
}
