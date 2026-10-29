using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>
/// Applies a whole-request save to a loaded request (stale check, client check, details and item list, machine
/// registration) without saving, so the caller can write it together with other changes in one transaction.
/// </summary>
internal sealed class RequestDetailsEditor(IRequestRepository requestRepository, IRequestEquipmentRegistrar? equipmentRegistrar)
{
    public async Task<RequestItemChanges> ApplyAsync(
        InspectionRequest request,
        UpdateRequestDetailsRequest details,
        CancellationToken cancellationToken)
    {
        // A screen that read an older version must not overwrite (or delete) what changed in the meantime.
        if (details.ExpectedUpdatedAt is DateTimeOffset expected && Math.Abs((request.UpdatedAt - expected).TotalMilliseconds) > 1)
            throw new AuthException(
                "request_changed",
                "The request was changed after it was loaded. Reload it and save again.",
                409);
        if (details.ClientId is Guid clientId)
        {
            if (!await requestRepository.ClientExistsAsync(clientId, cancellationToken))
                throw new KeyNotFoundException("Client was not found.");
            if (await requestRepository.GetClientManagerIdAsync(clientId, cancellationToken) != request.ManagerId)
                throw new AuthException("forbidden", "The client does not belong to the request team.", 403);
        }

        var changes = request.Revise(
            details.ClientName,
            details.Location,
            details.ClientId,
            details.PreferredDate,
            details.AccessNotes,
            details.Items?.Select(item => new RequestItemSpec(
                item.Id, item.Name, item.Quantity, item.EquipmentId, item.Type, item.Capacity,
                item.SerialNumber, item.Location, item.IssueDescription)).ToArray());
        if (equipmentRegistrar is not null)
            await equipmentRegistrar.RegisterAsync(request, cancellationToken);
        return changes;
    }
}
