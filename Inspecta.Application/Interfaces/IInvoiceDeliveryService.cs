using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>Generates the invoice PDF and emails it to the client from the coordinator's mailbox.</summary>
public interface IInvoiceDeliveryService
{
    /// <summary>The email that would go out, with the coordinator's edits applied. Sends nothing and changes nothing.</summary>
    Task<QuoteEmailPlan> PlanAsync(Invoice invoice, InspectionRequest request, QuoteEmailDraft? draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the invoice PDF and answers what was sent. Nothing is recorded on the invoice here: the caller does that only after this returns,
    /// so a failed send leaves the invoice as it was.
    /// </summary>
    /// <exception cref="Exceptions.EmailDeliveryException">The mail server rejected or could not receive the message.</exception>
    Task<QuoteEmailPlan> SendAsync(Invoice invoice, InspectionRequest request, QuoteEmailDraft? draft, CancellationToken cancellationToken = default);
}
