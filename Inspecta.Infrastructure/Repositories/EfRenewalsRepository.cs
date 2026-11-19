using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Executive;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

/// <summary>
/// The renewals of a company. Every query names the company (<c>ManagerId</c>) itself. The screens read through the company filters; only the three lookups of the
/// mailbox import (no signed-in user) bypass them, and they are limited by the company id.
/// </summary>
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
        // The date window is applied here, not in SQL: the tests run on SQLite, which cannot compare dates that carry an offset, and what is read is one company's dated machines as small rows.
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

    public async Task<RenewalClientInfo?> GetClientAsync(Guid managerId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await dbContext.Clients.AsNoTracking()
            .Where(item => item.Id == clientId && item.ManagerId == managerId && item.ArchivedAt == null)
            .Select(item => new { item.Id, item.Name, item.Location, item.CertificatesEmail })
            .SingleOrDefaultAsync(cancellationToken);
        if (client is null) return null;
        var contacts = await dbContext.ClientContacts.AsNoTracking()
            .Where(contact => contact.ClientId == clientId && contact.Email != null && contact.Email != "")
            .OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Name)
            .Select(contact => new RenewalContactRow(contact.Id, contact.Name, contact.Role, contact.Email!))
            .ToListAsync(cancellationToken);
        return new RenewalClientInfo(client.Id, client.Name, client.Location, client.CertificatesEmail, contacts);
    }

    public async Task<IReadOnlyList<RenewalMachineInfo>> GetMachinesAsync(Guid managerId, IReadOnlyList<Guid> equipmentIds, CancellationToken cancellationToken = default)
    {
        if (equipmentIds.Count == 0) return [];
        var ids = equipmentIds.ToArray();
        return await dbContext.Items.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.Client!.ManagerId == managerId)
            .Select(item => new RenewalMachineInfo(
                item.Id, item.ClientId, item.Name, item.SerialNumber, item.Capacity, item.Status == EquipmentStatus.Retired, item.IsArchived || item.ArchivedAt != null,
                item.NextInspectionDueAt, item.LastInspectionAt, item.LastInspectedOn))
            .ToListAsync(cancellationToken);
    }

    public Task<RenewalReminder?> GetReminderBySendKeyAsync(Guid managerId, Guid sendKey, CancellationToken cancellationToken = default) =>
        dbContext.RenewalReminders.SingleOrDefaultAsync(item => item.ManagerId == managerId && item.SendKey == sendKey, cancellationToken);

    public Task<RenewalReminder?> GetReminderAsync(Guid managerId, Guid reminderId, CancellationToken cancellationToken = default) =>
        dbContext.RenewalReminders.SingleOrDefaultAsync(item => item.ManagerId == managerId && item.Id == reminderId, cancellationToken);

    public async Task<bool> TryAddReminderAsync(RenewalReminder reminder, CancellationToken cancellationToken = default)
    {
        dbContext.RenewalReminders.Add(reminder);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (dbContext.RenewalReminders.AsNoTracking()
                                            .Any(item => item.ManagerId == reminder.ManagerId && item.SendKey == reminder.SendKey && item.Id != reminder.Id))
        {
            // The unique key refused it: the other click wrote the reminder first.
            dbContext.Entry(reminder).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Someone else changed the reminder first: forget this attempt's changes.
            foreach (var entry in exception.Entries) entry.State = EntityState.Detached;
            return false;
        }
    }

    public Task ReloadReminderAsync(RenewalReminder reminder, CancellationToken cancellationToken = default) =>
        dbContext.Entry(reminder).ReloadAsync(cancellationToken);

    public async Task<bool> CloseReminderAsync(Guid managerId, Guid reminderId, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        await dbContext.RenewalReminders
            .Where(item => item.ManagerId == managerId && item.Id == reminderId && item.RequestId == null && item.ClosedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ClosedAt, at)
                .SetProperty(item => item.ClosedById, actorId)
                .SetProperty(item => item.UpdatedAt, at), cancellationToken) == 1;

    public async Task<bool> ClaimReminderAsync(Guid managerId, Guid reminderId, Guid requestId, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        await dbContext.RenewalReminders
            .Where(item => item.ManagerId == managerId && item.Id == reminderId && item.RequestId == null && item.ClosedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RequestId, requestId)
                .SetProperty(item => item.RequestCreatedAt, at)
                .SetProperty(item => item.RequestCreatedById, actorId)
                .SetProperty(item => item.UpdatedAt, at), cancellationToken) == 1;

    public async Task<(IReadOnlyList<RenewalReminder> Items, int Total)> ListRemindersAsync(
        Guid managerId, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.RenewalReminders.AsNoTracking()
            .Where(item => item.ManagerId == managerId && (clientId == null || item.ClientId == clientId));
        // Ordered after loading small rows (an id and a date): the date carries an offset, which not every database can sort. Only the page is read in full.
        var keys = await query.Select(item => new { item.Id, item.CreatedAt }).ToListAsync(cancellationToken);
        var pageIds = keys.OrderByDescending(item => item.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.Id).ToList();
        var rows = (await query.Where(item => pageIds.Contains(item.Id)).ToListAsync(cancellationToken)).ToDictionary(item => item.Id);
        return (pageIds.Select(id => rows[id]).ToList(), keys.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, RenewalReminder>> LatestByClientAsync(
        Guid managerId, IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken = default)
    {
        if (clientIds.Count == 0) return new Dictionary<Guid, RenewalReminder>();
        var ids = clientIds.ToArray();
        // The newest of each client is chosen on small rows (an id and a date); only those few are read in full.
        var keys = await dbContext.RenewalReminders.AsNoTracking()
            .Where(item => item.ManagerId == managerId && ids.Contains(item.ClientId))
            .Select(item => new { item.Id, item.ClientId, item.CreatedAt })
            .ToListAsync(cancellationToken);
        var latestIds = keys.GroupBy(item => item.ClientId).Select(group => group.OrderByDescending(item => item.CreatedAt).First().Id).ToList();
        var rows = await dbContext.RenewalReminders.AsNoTracking().Where(item => latestIds.Contains(item.Id)).ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.ClientId);
    }

    public async Task<IReadOnlyList<RenewalReminderReply>> RepliesAsync(IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken = default)
    {
        if (reminderIds.Count == 0) return [];
        var ids = reminderIds.ToArray();
        return await dbContext.RenewalReminderReplies.AsNoTracking().Where(reply => ids.Contains(reply.ReminderId)).ToListAsync(cancellationToken);
    }

    public async Task<RenewalReminder?> FindReminderByMessageIdAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0) return null;
        var ids = messageIds.ToArray();
        // Any state: the id is random and ours, so a match is a reply to our email. The newest wins; only a few can match.
        return (await dbContext.RenewalReminders.IgnoreQueryFilters()
                .Where(item => item.ManagerId == managerId && ids.Contains(item.MessageId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
    }

    public Task<bool> IsOwnReminderMessageAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) =>
        dbContext.RenewalReminders.IgnoreQueryFilters().AnyAsync(item => item.ManagerId == managerId && item.MessageId == messageId, cancellationToken);

    public Task<bool> ReplyExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) =>
        dbContext.RenewalReminderReplies.IgnoreQueryFilters().AnyAsync(reply => reply.ManagerId == managerId && reply.MessageId == messageId, cancellationToken);

    public async Task AddReplyAsync(RenewalReminderReply reply, RenewalReminder reminder, CancellationToken cancellationToken = default)
    {
        dbContext.RenewalReminderReplies.Add(reply);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // The reply and the reminder stay tracked otherwise, and every later save of the same mailbox run would fail with them.
            foreach (var entry in exception.Entries) entry.State = EntityState.Detached;
            dbContext.Entry(reply).State = EntityState.Detached;
            dbContext.Entry(reminder).State = EntityState.Detached;
            throw;
        }
    }
}
