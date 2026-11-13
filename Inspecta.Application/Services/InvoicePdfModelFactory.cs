using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public static class InvoicePdfModelFactory
{
    public static InvoicePdfModel Create(Invoice invoice, Client? client, CompanyBranding? company, DateOnly today)
    {
        // A profile that keeps the built-in details prints the built-in company; its tax details are still its own.
        var own = company is { UsesBuiltInDetails: false };
        var lines = invoice.GetItems().Select(item => new InvoicePdfLine(
            item.Type,
            string.Join(" - ", new[] { item.Description, item.PrNumber is null ? null : $"PR {item.PrNumber}" }.Where(value => !string.IsNullOrWhiteSpace(value))),
            item.Quantity,
            item.PricingUnit,
            item.UnitPricePiastres,
            item.TotalPiastres)).ToList();
        return new InvoicePdfModel(
            invoice.Number,
            today,
            own ? company!.Name : null,
            Clean(company?.TaxRegistrationNumber),
            Clean(company?.ActivityCode),
            own ? Clean(company!.Address) : null,
            Clean(invoice.QuotationReference),
            invoice.ClientName,
            Clean(client?.TaxNumber),
            Clean(client?.Address),
            lines,
            invoice.SubtotalPiastres,
            invoice.DiscountPercent,
            invoice.DiscountPiastres,
            invoice.VatPercent,
            invoice.VatPiastres,
            invoice.TotalPiastres,
            invoice.PaidPiastres,
            invoice.BalancePiastres,
            company);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
