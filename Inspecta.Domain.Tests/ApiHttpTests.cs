using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>The whole API started in memory (SQLite instead of PostgreSQL): what a caller really gets over HTTP.</summary>
public sealed class ApiHttpTests : IClassFixture<ApiHttpTests.Api>
{
    public const string Password = "Correct-Horse-Battery-1";
    private readonly Api api;

    public ApiHttpTests(Api api) => this.api = api;

    public sealed class Api : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public Guid ManagerA { get; private set; }
        public Guid ManagerB { get; private set; }
        public Guid PagedClientId { get; private set; }

        public Api() => connection.Open();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Inspecta", "Host=unused;Database=unused;Username=unused;Password=unused");
            builder.UseSetting("Jwt:SigningKey", "test-only-signing-key-0123456789-abcdefghij");
            builder.UseSetting("IMAP_ENCRYPTION_KEY", Convert.ToBase64String(Enumerable.Range(1, 32).Select(index => (byte)index).ToArray()));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(value => value.ServiceType == typeof(DbContextOptions<InspectaDbContext>)).ToList())
                    services.Remove(descriptor);
                services.AddDbContext<InspectaDbContext>(options => options.UseSqlite(connection));
            });
        }

        private readonly object seedLock = new();

        /// <summary>Starts the host and seeds the shared teams once, whichever test class asks first.</summary>
        public void EnsureSeeded() { lock (seedLock) if (ManagerA == Guid.Empty) Seed(); }

        /// <summary>The company a technical manager heads (same id, as migrated companies are), with the manager as its default reviewer.</summary>
        public static Company CompanyOf(AppUser manager)
        {
            var company = Company.Create(manager.FullName, id: manager.Id);
            company.SetDefaultReviewer(manager.Id);
            return company;
        }

        /// <summary>Adds one more account (the shared test password), for a test that needs a user nobody else touches.</summary>
        public AppUser AddUser(string email, UserRole role, Guid? managerId = null)
        {
            EnsureSeeded();
            var options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
            var admin = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
            };
            using var db = new InspectaDbContext(options, new CurrentTenant(admin));
            var user = AppUser.Create(email, "Test " + email, role, managerId: managerId);
            user.SetPasswordHash(new IdentityPasswordService().Hash(user, Password));
            db.Users.Add(user);
            // A technical manager is the head of a company with the same id (how companies that existed before the Companies table were migrated).
            if (role == UserRole.TechnicalManager && managerId is null) db.Companies.Add(CompanyOf(user));
            db.SaveChanges();
            return user;
        }

        /// <summary>Writes rows straight to the database (as an admin, outside any tenant), for a test that needs a team's records to exist.</summary>
        public void SeedAs(Action<InspectaDbContext> seed)
        {
            EnsureSeeded();
            var options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
            var admin = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
            };
            using var db = new InspectaDbContext(options, new CurrentTenant(admin));
            seed(db);
            db.SaveChanges();
        }

        public void Seed()
        {
            _ = Services; // starts the host, which creates the schema
            var options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
            var admin = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
            };
            using var db = new InspectaDbContext(options, new CurrentTenant(admin));
            AppUser Manager(string email)
            {
                var user = AppUser.Create(email, "Manager " + email, UserRole.TechnicalManager);
                user.SetPasswordHash(new IdentityPasswordService().Hash(user, Password));
                return user;
            }

            var a = Manager("manager-a@example.com");
            var b = Manager("manager-b@example.com");
            var c = Manager("manager-c@example.com");
            db.Users.AddRange(a, b, c);
            db.Companies.AddRange(new[] { a, b, c }.Select(CompanyOf));
            db.SaveChanges();
            var pagedClients = Enumerable.Range(1, 5).Select(number => Client.Create($"Paged client {number}", managerId: c.Id)).ToList();
            db.Clients.AddRange(pagedClients);
            PagedClientId = pagedClients[0].Id;
            db.IssuedCertificateFiles.AddRange(Enumerable.Range(1, 3).Select(number => IssuedCertificateFile.Create(
                Guid.NewGuid(), Guid.NewGuid(), c.Id, pagedClients[0].Id, pagedClients[0].Name, "Crane", $"CERT-2026-1{number:000}", $"B1B2C3D4E5F6071{number}",
                new DateOnly(2026, 11, number), new DateOnly(2027, 11, number), $"crane-{number}.pdf", [1, 2, 3], new DateTimeOffset(2026, 11, number, 9, 0, 0, TimeSpan.Zero))));
            db.Clients.AddRange(Client.Create("Client of team A", managerId: a.Id), Client.Create("Client of team B", managerId: b.Id));
            db.SaveChanges();
            db.IssuedCertificateFiles.Add(IssuedCertificateFile.Create(
                Guid.NewGuid(), Guid.NewGuid(), c.Id, null, "<script>alert(1)</script> Ltd", "Forklift", "CERT-2026-0001", "A1B2C3D4E5F60718",
                new DateOnly(2026, 11, 1), new DateOnly(2030, 11, 1), "cert.pdf", [37, 80, 68, 70, 45, 49, 46, 52]));
            db.SaveChanges();
            // Each company is handed over with its company admin, who manages the accounts (a technical manager no longer does).
            foreach (var (head, name) in new[] { (a, "a"), (b, "b"), (c, "c") })
            {
                var companyAdmin = AppUser.Create($"company-admin-{name}@example.com", $"Company admin {name}", UserRole.CompanyAdmin, managerId: head.Id);
                companyAdmin.SetPasswordHash(new IdentityPasswordService().Hash(companyAdmin, Password));
                db.Users.Add(companyAdmin);
            }
            db.SaveChanges();
            (ManagerA, ManagerB) = (a.Id, b.Id);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }

    private static readonly object SeedLock = new();

    private HttpClient Http()
    {
        lock (SeedLock) if (api.ManagerA == Guid.Empty) api.Seed();
        return api.CreateClient();
    }

    private static async Task<string> TokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task ATeamSeesOnlyItsOwnClientsOverHttp()
    {
        var client = Http();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(client, "manager-a@example.com"));

        var names = (await client.GetFromJsonAsync<JsonElement>("/api/v1/clients")).EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()).ToArray();

        Assert.Equal(["Client of team A"], names);
    }

    [Fact]
    public async Task AnotherTeamsClientIsNotFoundAndNoTokenIsRefused()
    {
        var client = Http();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(client, "manager-a@example.com"));
        var others = await client.GetFromJsonAsync<JsonElement>("/api/v1/clients?includeArchived=true");
        Assert.DoesNotContain(others.EnumerateArray(), item => item.GetProperty("name").GetString() == "Client of team B");

        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/clients")).StatusCode);
    }

    [Fact]
    public async Task EveryResponseCarriesTheSecurityHeadersAndSwaggerIsServed()
    {
        var client = Http();
        var response = await client.GetAsync("/api/v1/clients");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task ARepeatedWrongPasswordIsLockedOutWithA429()
    {
        var client = Http();
        HttpStatusCode last = default;
        string? code = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "lock-me@example.com", password = "wrong" });
            last = response.StatusCode;
            if (last == HttpStatusCode.TooManyRequests) code = await response.Content.ReadAsStringAsync();
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.Contains("too_many_attempts", code);
    }
}
