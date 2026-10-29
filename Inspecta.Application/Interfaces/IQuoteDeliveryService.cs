using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>Generates the quotation PDF and emails it to whoever sent the original request.</summary>
public interface IQuoteDeliveryService
{
    /// <summary>
    /// Sends the quotation PDF as a reply to the email the request came from. Returns <c>null</c> when the request has
    /// no source email (a manual request), in which case nothing is sent. The caller changes the quote and request
    /// state only after this returns, so a failed delivery leaves them untouched.
    /// </summary>
    /// <exception cref="Exceptions.EmailDeliveryException">The mail server rejected or could not receive the message.</exception>
    Task<OutboundEmail?> DeliverAsync(
        InspectionRequest request,
        Quote quote,
        OutboundEmailKind kind,
        CancellationToken cancellationToken = default);
}
