using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Pdf;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>The identity of a company: what is stored, who may change it, and that the PDFs and emails carry it.</summary>
public sealed class CompanyProfileTests : IDisposable
{
    // A 1 x 1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("manager@acme.com", "Acme Manager", UserRole.TechnicalManager);
    private readonly AppUser coordinator = AppUser.Create("coordinator@acme.com", "Acme Coordinator", UserRole.Coordinator);

    public CompanyProfileTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = Context();
        db.Database.EnsureCreated();
        db.AddRange(manager, coordinator);
        db.SaveChanges();
    }

    private InspectaDbContext Context() => new(options, new CurrentTenant(new HttpContextAccessor()));

    private static SaveCompanyProfileRequest Request() => new()
    {
        Name = "Acme Inspection Co.",
        CertificateTitle = "Acme Inspection",
        IssuerName = "Acme Inspection Services",
        Tagline = "Safety & Quality Services.",
        Address = "5 Industrial Zone, Cairo, Egypt",
        Phones = ["+20 100 111 2222"],
        Emails = ["info@acme.com", "certs@acme.com"],
        Website = "www.acme.com",
        SignerName = "Mona Hassan",
        PrimaryColor = "#6e2896",
        AccentColor = "#F28C28",
        LogoBase64 = Convert.ToBase64String(Png)
    };

    [Fact]
    public async Task AProfileIsSavedChangedAndItsImagesKeptOrRemovedOnRequest()
    {
        await using var db = Context();
        var service = new CompanyProfileService(new EfCompanyProfileRepository(db), new EfUserRepository(db));
        Assert.Null(await service.GetAsync(manager.Id));

        var saved = await service.SaveAsync(manager.Id, Request());

        Assert.Equal("Acme Inspection Co.", saved.Name);
        Assert.Equal("#6E2896", saved.PrimaryColor);
        Assert.Equal(["info@acme.com", "certs@acme.com"], saved.Emails);
        Assert.True(saved.HasLogo);
        Assert.False(saved.HasAccreditations);
        Assert.Equal($"/api/v1/branding/{manager.Id}/logo.png", saved.LogoUrl);

        // A second save replaces the text and keeps the logo that was left out.
        await using (var again = Context())
        {
            var changed = await new CompanyProfileService(new EfCompanyProfileRepository(again), new EfUserRepository(again))
                .SaveAsync(manager.Id, Request() with { Name = "Acme Group", LogoBase64 = null });
            Assert.Equal("Acme Group", changed.Name);
            Assert.True(changed.HasLogo);
        }

        // An empty text removes it.
        await using (var removing = Context())
        {
            var service2 = new CompanyProfileService(new EfCompanyProfileRepository(removing), new EfUserRepository(removing));
            Assert.False((await service2.SaveAsync(manager.Id, Request() with { LogoBase64 = string.Empty })).HasLogo);
            Assert.Null(await service2.GetLogoAsync(manager.Id));
        }

        await using var check = Context();
        Assert.Single(await check.CompanyProfiles.ToListAsync());
    }

    [Fact]
    public async Task OnlyATechnicalManagerCanHaveOneAndABadImageOrColourSavesNothing()
    {
        await using var db = Context();
        var service = new CompanyProfileService(new EfCompanyProfileRepository(db), new EfUserRepository(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveAsync(coordinator.Id, Request()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveAsync(Guid.NewGuid(), Request()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(manager.Id, Request() with { LogoBase64 = "not base64!" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
            manager.Id, Request() with { LogoBase64 = Convert.ToBase64String("%PDF not an image"u8.ToArray()) }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(manager.Id, Request() with { PrimaryColor = "blue" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(manager.Id, Request() with { Name = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
            manager.Id, Request() with { Emails = Enumerable.Range(0, 6).Select(index => $"a{index}@acme.com").ToArray() }));

        await using var check = Context();
        Assert.Empty(await check.CompanyProfiles.ToListAsync());
    }

    [Fact]
    public async Task ATeamWithAProfileGetsItsBrandingAndATeamWithoutOneGetsNone()
    {
        await using var db = Context();
        var service = new CompanyProfileService(new EfCompanyProfileRepository(db), new EfUserRepository(db));
        await service.SaveAsync(manager.Id, Request());

        var branding = await ((ICompanyBrandingProvider)service).GetAsync(manager.Id);
        Assert.Equal("Acme Inspection Services", branding!.IssuerName);
        Assert.Equal("Acme Inspection", branding.CertificateTitle);
        Assert.Equal("Mona Hassan", branding.SignerName);
        Assert.Equal("info@acme.com", branding.ToQuotationProfile().Email);
        Assert.Empty(branding.ToQuotationProfile().Services);

        Assert.Null(await ((ICompanyBrandingProvider)service).GetAsync(Guid.NewGuid()));
        Assert.Null(await ((ICompanyBrandingProvider)service).GetAsync(null));

        // Left out, the certificate title and issuer take the name.
        await service.SaveAsync(manager.Id, Request() with { CertificateTitle = null, IssuerName = null });
        var plain = await ((ICompanyBrandingProvider)service).GetAsync(manager.Id);
        Assert.Equal("Acme Inspection Co.", plain!.CertificateTitle);
        Assert.Equal("Acme Inspection Co.", plain.IssuerName);
    }

    [Fact]
    public void TheEmailSignatureAndItsLogoComeFromTheTeamsCompanyOrTheBuiltInOne()
    {
        var company = new CompanyBranding(
            "Acme", "Acme", "Acme", string.Empty, "Cairo", [], [], "www.acme.com", string.Empty, Png, null, "#112233", "#445566");
        var builtIn = new FixedBranding(new EmailBranding("Integrity", "Alexandria", "www.integrity-intl.com", null));

        Assert.Equal("Acme", EmailLayout.BrandingFor(company, builtIn)!.Get().CompanyName);
        Assert.Same(builtIn, EmailLayout.BrandingFor(null, builtIn));
        Assert.Null(EmailLayout.BrandingFor(null, null));

        var team = Guid.NewGuid();
        Assert.Equal($"/api/v1/branding/{team}/logo.png", EmailLayout.PreviewLogoFor(team, company));
        Assert.Equal(EmailLayout.PreviewLogoPath, EmailLayout.PreviewLogoFor(team, null));
        Assert.Equal(EmailLayout.PreviewLogoPath, EmailLayout.PreviewLogoFor(team, company with { LogoPng = null }));
    }

    [Fact]
    public void ACertificateAndAQuotationOfAnotherCompanyAreDrawnWithoutTheBuiltInIdentity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();
        var company = new CompanyBranding(
            "Acme Inspection Co.", "Acme Inspection", "Acme Inspection Services", "Safety.", "Cairo", ["+20 1"], ["info@acme.com"],
            "www.acme.com", "Mona Hassan", Png, null, "#6E2896", "#F28C28");

        var certificate = Certificate(company);
        var quotation = Quotation(company);

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(generator.Generate(certificate).Content, 0, 5));
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(generator.Generate(certificate with { Company = company with { LogoPng = null } }).Content, 0, 5));

        // Without the first customer's cover and texts, the quotation is a smaller document.
        var own = generator.Generate(quotation).Content.Length;
        var builtIn = generator.Generate(quotation with { Company = null, Branding = null }).Content.Length;
        Assert.True(own < builtIn, $"{own} should be smaller than {builtIn}");
    }

    [Fact]
    public void ADomainProfileKeepsOnlyWhatIsValid()
    {
        var profile = CompanyProfile.Create(Guid.NewGuid());
        profile.Update("Acme", null, null, null, "Cairo", ["+20 1", "+20 1", " "], ["a@acme.com"], null, null, null, null);

        Assert.Equal(["+20 1"], profile.Phones);
        Assert.Equal(CompanyProfile.DefaultPrimaryColor, profile.PrimaryColor);
        Assert.Throws<ArgumentException>(() => profile.SetLogo(new byte[CompanyProfile.MaxLogoBytes + 1]));
        Assert.Throws<ArgumentException>(() => profile.SetLogo([1, 2, 3]));
        profile.SetLogo(Png);
        Assert.NotNull(profile.LogoPng);
        profile.SetLogo(null);
        Assert.Null(profile.LogoPng);
        Assert.Throws<ArgumentException>(() => CompanyProfile.Create(Guid.Empty));
    }

    private sealed class FixedBranding(EmailBranding branding) : IEmailBranding
    {
        public EmailBranding Get() => branding;
    }

    private static CertificatePdfModel Certificate(CompanyBranding? company) => new(
        "CERT-2026-0001", DateTimeOffset.UtcNow, false, "Delta Steel Co.", "10th of Ramadan", "Ahmed",
        null, null, null, new DateOnly(2026, 10, 26), new DateOnly(2027, 10, 25), "Forklift Truck", ["ISO 2330"], "Visual",
        "Satisfactory", [new CertificatePdfField("Serial Number", "FK-100")],
        new CertificatePdfQuestionnaire(true, true, false, true, true, false, "NONE", false, false, null, "N/A", "NONE", null),
        [new CertificatePdfSection("Brakes", [new CertificatePdfRow("Service brake", "Yes", null)])],
        "Karim", "Lifts", "Mona", null, "https://example.com/verify/ABCD", company);

    private static QuotationPdfModel Quotation(CompanyBranding company) => new(
        "ACM/001/2026 rev01", "QT-2026-0001", "Delta Steel Co.", "Ahmed", "Forklift Truck", DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow.AddDays(30), 30, "Hager", "Coordinator", null,
        [new QuotationPdfItem("Forklift", "Forklift inspection", null, 2, 150000, "Unit")],
        ["Visual and function test"], ["Prices exclude VAT."], company.ToQuotationProfile(), company);

    public void Dispose() => connection.Dispose();
}
