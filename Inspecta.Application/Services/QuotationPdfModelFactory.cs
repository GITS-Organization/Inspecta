using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

internal static class QuotationPdfModelFactory
{
    private const int MaxSubjectLength = 80;

    // A draft previews (and is sent) as if issued now; a quotation that was already sent keeps its dates.
    public static QuotationPdfModel Create(Quote quote, InspectionRequest request, string preparedBy, DateTimeOffset now)
    {
        var issuedAt = quote.SentAt ?? now;
        var validUntil = quote.IsSent ? quote.ValidUntil : issuedAt.AddDays(quote.ValidForDays);
        var items = quote.GetItems();
        return new QuotationPdfModel(
            quote.ProposalNumber,
            quote.QuotationNumber ?? $"QT-{quote.CreatedAt.Year}-{quote.Id.ToString("N")[..6].ToUpperInvariant()}",
            quote.ClientName ?? request.ClientName,
            quote.ContactName,
            Subject(items),
            issuedAt,
            validUntil,
            quote.ValidForDays,
            preparedBy,
            QuotationDefaults.PreparedByTitle,
            quote.Message,
            items
                .Select(item => new QuotationPdfItem(
                    item.Type, item.Description ?? item.Type, item.PrNumber, item.Quantity, item.UnitPricePiastres, item.PricingUnit))
                .ToArray(),
            quote.GetScopeOfWork(),
            quote.GetNotes());
    }

    // "Mobile crane, Air receiver": the distinct item types in order.
    private static string Subject(IReadOnlyList<QuoteItem> items)
    {
        var subject = string.Join(", ", items.Select(item => item.Type).Distinct(StringComparer.OrdinalIgnoreCase));
        return subject.Length <= MaxSubjectLength ? subject : subject[..(MaxSubjectLength - 1)].TrimEnd() + "…";
    }
}
