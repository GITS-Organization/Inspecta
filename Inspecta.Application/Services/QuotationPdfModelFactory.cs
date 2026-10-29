using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

internal static class QuotationPdfModelFactory
{
    // A draft previews (and is sent) as if issued now; a quotation that was already sent keeps its dates.
    public static QuotationPdfModel Create(Quote quote, InspectionRequest request, DateTimeOffset now)
    {
        var issuedAt = quote.SentAt ?? now;
        var validUntil = quote.IsSent ? quote.ValidUntil : issuedAt.AddDays(quote.ValidForDays);
        return new QuotationPdfModel(
            quote.ProposalNumber,
            quote.ClientName ?? request.ClientName,
            issuedAt,
            validUntil,
            quote.Message,
            quote.GetItems()
                .Select(item => new QuotationPdfItem(
                    item.Description ?? item.Type, item.PrNumber, item.Quantity, item.UnitPricePiastres, item.PricingUnit))
                .ToArray(),
            quote.GetScopeOfWork(),
            quote.GetNotes());
    }
}
