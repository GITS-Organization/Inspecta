using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Executive;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfRenewalsRepository(InspectaDbContext dbContext) : IRenewalsRepository
{
    public async Task<IReadOnlyList<RenewalClientRows>> ListDueAsync(Guid managerId, DateTimeOffset dueBefore, CancellationToken cancellationToken = default)
    {
        // Small projected rows of one company: the machines that are active, not archived, of a client that is not archived, with a next inspection date.
        var machines = await dbContext.Items.AsNoTracking()
            .Where(item => item.Client!.ManagerId == managerId && item.Client.ArchivedAt == null
                && item.NextInspectionDueAt != null
                && item.Status == EquipmentStatus.Active && !item.IsArchived && item.ArchivedAt == null)
            .Select(item => new { item.ClientId, item.Id, item.Name, item.SerialNumber, item.Capacity, item.LastInspectionAt, item.LastInspectedOn, Due = item.NextInspectionDueAt!.Value })
            .ToListAsync(cancellationToken);
        // The date window is applied here: the machines of one company are few, and a comparison of dates with offsets does not translate on every database.
        machines = machines.Where(machine => machine.Due < dueBefore).ToList();
        if (machines.Count == 0) return [];

        var clientIds = machines.Select(machine => machine.ClientId).Distinct().ToList();
        var clients = await dbContext.Clients.AsNoTracking()
            .Where(client => clientIds.Contains(client.Id) && client.ManagerId == managerId)
            .Select(client => new { client.Id, client.Name, client.Location, client.CertificatesEmail })
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.ClientContacts.AsNoTracking()
            .Where(contact => clientIds.Contains(contact.ClientId) && contact.Email != null && contact.Email != "")
            .OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Name)
            .Select(contact => new { contact.ClientId, contact.Id, contact.Name, contact.Role, Email = contact.Email! })
            .ToListAsync(cancellationToken);

        // The machines already on a request that is still open: a machine is on one request at a time.
        var machineIds = machines.Select(machine => machine.Id).ToList();
        var finished = RenewalsFigures.FinishedStatuses.ToList();
        var onOpenRequest = (await dbContext.RequestItems.AsNoTracking()
            .Where(item => item.EquipmentId != null && machineIds.Contains(item.EquipmentId.Value)
                && item.Request!.ManagerId == managerId && item.Request.Triage != RequestTriage.Maybe && !finished.Contains(item.Request.Status))
            .Select(item => item.EquipmentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        return clients.Select(client => new RenewalClientRows(
            client.Id, client.Name, client.Location, client.CertificatesEmail,
            machines.Where(machine => machine.ClientId == client.Id)
                .Select(machine => new RenewalMachineRow(
                    machine.Id, machine.Name, machine.SerialNumber, machine.Capacity, machine.LastInspectionAt, machine.LastInspectedOn,
                    machine.Due, onOpenRequest.Contains(machine.Id))).ToList(),
            contacts.Where(contact => contact.ClientId == client.Id)
                .Select(contact => new RenewalContactRow(contact.Id, contact.Name, contact.Role, contact.Email)).ToList())).ToList();
    }
}
