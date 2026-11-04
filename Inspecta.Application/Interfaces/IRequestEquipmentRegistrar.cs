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

    /// <summary>
    /// Registers these machines of a job that is already under way (found on site) under the job's client, so they are in the client's
    /// list at once and can be certified, and links each to its record. Gears are left alone. Nothing is saved here.
    /// </summary>
    Task RegisterMachinesAsync(InspectionRequest request, IReadOnlyCollection<RequestItem> machines, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
