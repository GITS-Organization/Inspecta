using System.Security.Claims;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Runs the real repositories against a real (SQLite) database. The fakes used elsewhere cannot show how EF Core tracks
/// new children that carry client-generated keys, which is what these tests are for.
/// </summary>
public sealed class PersistenceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
    private readonly AppUser coordinator;
    private Guid requestId;

    public PersistenceTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        coordinator = AppUser.Create("coordinator@example.com", "Coordinator", UserRole.Coordinator, managerId: manager.Id);
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Users.AddRange(manager, coordinator);
        var request = InspectionRequest.CreateFromEmail(manager.Id, coordinator.Id, new IncomingEmail(
            "42", 1, "Please quote.", null, "buyer@alnoor.com", "Al Noor", "Quote request",
            [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)));
        db.Requests.Add(request);
        db.SaveChanges();
        requestId = request.Id;
    }

    [Fact]
    public async Task AStatusChangeIsSavedWithItsHistoryRow()
    {
        await using var db = CreateContext();
        var service = new QuoteService(
            new EfQuoteRepository(db), new EfRequestRepository(db), null!, null!, new EfOutboundEmailRepository(db), new EfUserRepository(db));

        await service.RejectRequestAsync(requestId, coordinator.Id, "coordinator", new RejectRequestRequest { Reason = "Out of scope" });

        await using var check = CreateContext();
        Assert.Equal(JobStatus.QuoteRejected, check.Requests.AsNoTracking().Single().Status);
        Assert.Equal(2, check.Set<JobStatusHistory>().Count());
    }

    [Fact]
    public async Task OneSaveWritesTheRequestItsItemsTheNewMachineTheHistoryAndTheQuotation()
    {
        await using var db = CreateContext();
        var requests = new EfRequestRepository(db);
        var quotes = new EfQuoteRepository(db);
        var request = (await requests.GetByIdAsync(requestId, tracking: true))!;

        var changes = request.Revise("Al Noor", "Cairo", null, null, null,
            [new RequestItemSpec(null, "Crane", 1, null, "Crane", "5 ton", null, "Cairo", null)]);
        request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(14), coordinator.Id);
        requests.StageRevision(changes);
        await quotes.AddAsync(quote);

        await using var check = CreateContext();
        var saved = check.Requests.AsNoTracking().Single();
        Assert.Equal(JobStatus.QuoteDraft, saved.Status);
        Assert.Equal(1, check.RequestItems.Count());
        Assert.Equal(1, check.Quotes.Count());
        Assert.Equal(2, check.Set<JobStatusHistory>().Count());
    }

    [Fact]
    public async Task UnfinishedJobsAreCountedPerInspector()
    {
        var inspector = AppUser.Create("inspector@example.com", "Inspector", UserRole.Inspector, managerId: manager.Id);
        await using (var seed = CreateContext())
        {
            seed.Users.Add(inspector);
            var request = seed.Requests.Include(item => item.Items).Single();
            request.UpdateDetails("Al Noor", "Cairo", null, null, null);
            request.AddItem(RequestItem.Create("Crane", 1));
            request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
            request.TransitionTo(JobStatus.QuoteSent, coordinator.Id);
            request.TransitionTo(JobStatus.QuoteAccepted, coordinator.Id);
            request.AssignInspector(inspector.Id, DateTimeOffset.UtcNow.AddDays(1), coordinator.Id);
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var counts = await new EfRequestRepository(db).CountActiveByInspectorAsync([inspector.Id, Guid.NewGuid()]);

        Assert.Equal(1, counts[inspector.Id]);
        Assert.Single(counts);
    }

    private InspectaDbContext CreateContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test"))
            }
        };
        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    public void Dispose() => connection.Dispose();
}
