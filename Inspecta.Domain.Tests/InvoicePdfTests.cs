using System.Text;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>The invoice document: what its model carries, what it leaves out when a detail is missing, and that it renders.</summary>
public sealed class InvoicePdfTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    private static CompanyBranding Branding(bool builtIn, string? tax = "100-200-300", string? activity = "4610", string address = "5 Nile St, Cairo") => new(
        "Gamma Inspection", "Gamma Inspection", "Gamma Inspection", "", address, [], [], "", "", null, null, "#184896", "#34A046",
        UsesBuiltInDetails: builtIn, TaxRegistrationNumber: tax, ActivityCode: activity);

    private static Invoice PaidInvoice(long paid = 300_000)
    {
        var (_, _, invoice) = InvoiceTestData.Make(Guid.NewGuid(), 1_000_000, "Alexandria Steel", acceptedOn: new DateOnly(2026, 11, 1));
        if (paid > 0) invoice.RecordPayment(paid, Today, null, InvoiceTestData.Actor, Today);
        return invoice;
    }

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().AddInMemoryCollection().Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void The_model_carries_the_frozen_numbers_the_lines_and_what_is_paid()
    {
        var invoice = PaidInvoice();
        var client = Client.Create("Alexandria Steel", taxNumber: "555-666", address: "12 Port Said St, Alexandria");

        var model = InvoicePdfModelFactory.Create(invoice, client, Branding(builtIn: false), Today);

        Assert.Equal(invoice.Number, model.Number);
        Assert.Equal("Gamma Inspection", model.IssuerName);
        Assert.Equal("100-200-300", model.IssuerTaxRegistrationNumber);
        Assert.Equal("4610", model.IssuerActivityCode);
        Assert.Equal("5 Nile St, Cairo", model.IssuerAddress);
        Assert.Equal("555-666", model.ClientTaxNumber);
        Assert.Equal("12 Port Said St, Alexandria", model.ClientAddress);
        var line = Assert.Single(model.Lines);
        Assert.Equal("Crane", line.CodeName);
        Assert.Equal("Crane inspection", line.Description);
        Assert.Equal(1_000_000, model.TotalPiastres);
        Assert.Equal(300_000, model.PaidPiastres);
        Assert.Equal(700_000, model.BalancePiastres);
    }

    [Fact]
    public void A_missing_detail_is_null_so_the_page_leaves_its_line_out()
    {
        var model = InvoicePdfModelFactory.Create(PaidInvoice(0), client: null, Branding(builtIn: false, tax: " ", activity: null), Today);

        Assert.Null(model.IssuerTaxRegistrationNumber);
        Assert.Null(model.IssuerActivityCode);
        Assert.Null(model.ClientTaxNumber);
        Assert.Null(model.ClientAddress);
    }

    [Fact]
    public void A_profile_with_the_built_in_details_prints_the_built_in_company_but_keeps_its_tax_numbers()
    {
        var model = InvoicePdfModelFactory.Create(PaidInvoice(0), null, Branding(builtIn: true), Today);

        Assert.Null(model.IssuerName);
        Assert.Null(model.IssuerAddress);
        Assert.Equal("100-200-300", model.IssuerTaxRegistrationNumber);
    }

    [Fact]
    public void The_pdf_renders_valid_bytes_with_a_safe_file_name()
    {
        using var provider = Provider();
        var model = InvoicePdfModelFactory.Create(PaidInvoice(), null, null, Today);

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(model);

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
        Assert.True(file.Content.Length > 800);
        Assert.Equal($"invoice-{model.Number}.pdf", file.FileName);
    }

    [Fact]
    public void The_pdf_still_renders_with_every_optional_detail_missing_and_with_vat_and_discount()
    {
        using var provider = Provider();
        var withVat = new InvoicePdfModel(
            "INV-2026-ABCDEF", Today, null, null, null, null, null, "Client", null, null,
            [new InvoicePdfLine("Crane", "", 1, "Per Visit", null, 1_234_500)], 1_234_500, 10m, 123_450, 14m, 155_555, 1_266_605, 0, 1_266_605);

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(withVat);

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
    }
}
