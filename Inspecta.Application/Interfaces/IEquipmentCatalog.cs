using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>
/// Lookups and staging for the clients and machines (equipment) a request is registered against. Staged records are not
/// saved here: they are written by the same save as the request, so the request and its new records land together.
/// </summary>
public interface IEquipmentCatalog
{
    /// <summary>An active client of the team with this name (ignoring case and extra spaces), if there is one.</summary>
    Task<Client?> FindClientByNameAsync(Guid managerId, string name, CancellationToken cancellationToken = default);

    /// <summary>An active machine of the client with this name, serial number and capacity (ignoring case), if there is one.</summary>
    Task<Item?> FindEquipmentAsync(
        Guid clientId,
        string name,
        string? serialNumber,
        string? capacity,
        CancellationToken cancellationToken = default);

    void Stage(Client client);

    void Stage(Item equipment);
}
