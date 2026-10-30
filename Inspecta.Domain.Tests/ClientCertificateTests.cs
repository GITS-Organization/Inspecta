using System.Security.Claims;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The certificates filed under a client and the public check behind the QR code.</summary>
public sealed class ClientCertificateTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
    private readonly Guid clientId;
    private readonly Guid fileId;

    public ClientCertificateTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext(role: "admin", tenant: null);
        db.Database.EnsureCreated();
        db.Users.Add(manager);
        var client = Client.Create("Al Noor Industries", managerId: manager.Id);
        db.Clients.Add(client);
        var file = IssuedCertificateFile.Create(
            Guid.NewGuid(), Guid.NewGuid(), manager.Id, client.Id, "Al Noor Industries", "Forklift Truck",
            "CERT-2026-0001", "A1B2C3D4E5F60718", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1),
            "certificate-CERT-2026-0001.pdf", [1, 2, 3, 4]);
        db.IssuedCertificateFiles.Add(file);
        db.SaveChanges();
        clientId = client.Id;
        fileId = file.Id;
    }

    [Fact]
    public async Task TheClientsCertificatesAreListedAndDownloadedExactlyAsFiled()
    {
        await using var db = CreateContext();
        var service = new ClientCertificateService(new EfClientRepository(db), new EfCertificateFileRepository(db));

        var listed = Assert.Single(await service.ListAsync(clientId));
        var pdf = await service.GetPdfAsync(clientId, fileId);

        Assert.Equal("CERT-2026-0001", listed.CertificateNumber);
        Assert.Equal("Forklift Truck", listed.ItemName);
        Assert.Equal(new DateOnly(2027, 10, 1), listed.NextExaminationDate);
        Assert.Equal(64, listed.Sha256.Length);
        Assert.Equal([1, 2, 3, 4], pdf.Content);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPdfAsync(clientId, Guid.NewGuid()));
    }

    [Fact]
    public async Task AnotherTeamDoesNotSeeTheCertificates()
    {
        await using var db = CreateContext(role: "technical_manager", tenant: Guid.NewGuid());
        var service = new ClientCertificateService(new EfClientRepository(db), new EfCertificateFileRepository(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListAsync(clientId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPdfAsync(clientId, fileId));
    }

    [Fact]
    public async Task TheQrCodeLeadsToACheckThatNeedsNoSignIn()
    {
        // The public page has no team: the lookup ignores the team filter.
        await using var db = CreateContext(role: "anonymous", tenant: null);
        var service = new ClientCertificateService(new EfClientRepository(db), new EfCertificateFileRepository(db));

        var valid = await service.VerifyAsync("a1b2c3d4e5f60718", new DateOnly(2026, 11, 1));
        Assert.True(valid.Valid);
        Assert.Equal("valid", valid.Status);
        Assert.Equal("CERT-2026-0001", valid.CertificateNumber);
        Assert.Equal("Al Noor Industries", valid.ClientName);
        Assert.Equal("Integrity International Inspection", valid.IssuedBy);

        var expired = await service.VerifyAsync("A1B2C3D4E5F60718", new DateOnly(2028, 1, 1));
        Assert.True(expired.Valid);
        Assert.Equal("expired", expired.Status);

        foreach (var wrong in new[] { "A1B2C3D4E5F60719", "short", "", null })
        {
            var unknown = await service.VerifyAsync(wrong, new DateOnly(2026, 11, 1));
            Assert.False(unknown.Valid);
            Assert.Equal("not_found", unknown.Status);
            Assert.Null(unknown.ClientName);
        }
    }

    private InspectaDbContext CreateContext(string role = "admin", Guid? tenant = null)
    {
        var claims = new List<Claim> { new("role", role) };
        if (tenant is { } id) claims.Add(new Claim("tenant_id", id.ToString()));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    public void Dispose() => connection.Dispose();
}
