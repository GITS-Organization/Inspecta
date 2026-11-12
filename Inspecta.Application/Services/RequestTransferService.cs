using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class RequestTransferService(
    IRequestRepository requestRepository,
    IQuoteRepository quoteRepository,
    IUserRepository userRepository) : IRequestTransferService
{
    public async Task<RequestResponse> TransferAsync(
        Guid requestId, Guid actorId, string role, TransferRequest transfer, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");

        // The coordinator that owns the request may hand it over until the client has been written to; the manager and the admin may
        // always (before it is finished), for example when the coordinator left or is away.
        if (role == "coordinator")
        {
            if (request.CoordinatorId != actorId)
                throw new AuthException("forbidden", "Only the coordinator that owns the request can transfer it.", 403);
            if ((await quoteRepository.ListForRequestAsync(requestId, cancellationToken)).Any(quote => quote.SentAt is not null))
                throw new AuthException(
                    "not_transferable", "A quotation was already sent from the current coordinator's mailbox, so the request stays with them. Ask the technical manager.", 409);
        }

        var target = await userRepository.GetByIdAsync(transfer.CoordinatorId, cancellationToken);
        if (target is null || target.ManagerId is null || target.ManagerId != request.ManagerId)
            throw new KeyNotFoundException("Coordinator was not found.");
        if (target.Role != UserRole.Coordinator)
            throw new AuthException("not_a_coordinator", "The request can only be handed to a coordinator.", 400);
        if (!target.IsActive)
            throw new AuthException("coordinator_inactive", "That coordinator's account is not active.", 400);
        if (target.Id == request.CoordinatorId)
            throw new AuthException("same_coordinator", "The request already belongs to this coordinator.", 400);

        var owner = request.CoordinatorId is { } ownerId ? await userRepository.GetByIdAsync(ownerId, cancellationToken) : null;
        try
        {
            request.TransferTo(target.Id, actorId, owner?.FullName ?? "the previous coordinator", target.FullName, transfer.Note);
        }
        catch (InvalidOperationException exception)
        {
            throw new AuthException("not_transferable", exception.Message, 409);
        }

        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    public async Task<IReadOnlyList<TeamCoordinatorResponse>> ListCoordinatorsAsync(
        Guid actorId, string role, Guid? managerId, CancellationToken cancellationToken = default)
    {
        var team = role switch
        {
            "admin" => managerId,
            "technical_manager" => actorId,
            _ => (await userRepository.GetByIdAsync(actorId, cancellationToken))?.ManagerId
        };
        if (team is null) return [];

        return (await userRepository.ListAsync(team, UserRole.Coordinator, isActive: true, cancellationToken: cancellationToken))
            .Where(user => user.Id != actorId)
            .OrderBy(user => user.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new TeamCoordinatorResponse(user.Id, user.FullName, user.Email))
            .ToArray();
    }
}
