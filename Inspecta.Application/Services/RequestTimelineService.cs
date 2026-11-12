using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

/// <summary>Reads everything stored about a request (nothing is written) and hands it to <see cref="RequestTimelineBuilder"/>.</summary>
public sealed class RequestTimelineService(
    IRequestRepository requestRepository,
    IQuoteRepository quoteRepository,
    IOutboundEmailRepository outboundEmailRepository,
    IClientReplyRepository clientReplyRepository,
    ICertificateDeliveryRepository deliveryRepository,
    IUserRepository userRepository) : IRequestTimelineService
{
    public async Task<RequestTimelineResponse?> GetAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) return null;

        var quotes = await quoteRepository.ListForRequestAsync(requestId, cancellationToken);
        var emails = await outboundEmailRepository.ListForRequestAsync(requestId, cancellationToken);
        var replies = await clientReplyRepository.ListForRequestAsync(requestId, cancellationToken);
        var delivery = await deliveryRepository.GetSentAsync(requestId, cancellationToken);

        var people = request.History.Select(entry => entry.ActorId)
            .Concat(request.ReviewComments.Select(comment => comment.ReviewerId))
            .Concat(quotes.Select(quote => quote.CreatedBy))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var names = await userRepository.GetFullNamesAsync(people, cancellationToken);

        return RequestTimelineBuilder.Build(
            new RequestTimelineSources(request, quotes, emails, replies, delivery, names), DateTimeOffset.UtcNow);
    }
}
