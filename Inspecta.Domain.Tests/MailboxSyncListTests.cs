using System.Security.Claims;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>Which mailboxes the background sync reads: a mailbox with a problem must be tried again, or it could never recover by itself.</summary>
public sealed class MailboxSyncListTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly Company company = Company.Create("Sync list company");

    public MailboxSyncListTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Companies.Add(company);
        db.SaveChanges();
    }

    private InspectaDbContext CreateContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
        };
        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    private Guid Mailbox(string name, Action<CoordinatorEmailConfig>? change = null)
    {
        var coordinator = AppUser.Create($"{name}@example.com", name, UserRole.Coordinator, managerId: company.Id);
        var config = CoordinatorEmailConfig.Create(coordinator.Id, company.Id, "imap.example.com", 993, $"{name}@example.com", "encrypted", true);
        change?.Invoke(config);
        using var db = CreateContext();
        db.Users.Add(coordinator);
        db.CoordinatorEmailConfigs.Add(config);
        db.SaveChanges();
        return coordinator.Id;
    }

    [Fact]
    public async Task A_mailbox_with_a_problem_is_still_read_but_one_that_must_be_connected_again_is_not()
    {
        var connected = Mailbox("connected");
        var problem = Mailbox("problem", config => config.RecordFailedCheck(DateTimeOffset.UtcNow, "The server did not answer.", "reach"));
        var reconnect = Mailbox("reconnect", config =>
        {
            config.MarkNeedsReconnect("Microsoft refused the saved sign-in.");
        });
        await using var db = CreateContext();

        // The sync query itself cannot run on SQLite (a date comparison), so the status rule it uses is the shared expression.
        var listed = (await db.CoordinatorEmailConfigs.Where(CoordinatorEmailConfig.IsReadableBySync).ToListAsync()).Select(config => config.CoordinatorId).ToList();

        Assert.Contains(connected, listed);
        Assert.Contains(problem, listed);
        Assert.DoesNotContain(reconnect, listed);
    }

    public void Dispose() => connection.Dispose();
}
