using System.Security.Claims;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The two guarded updates of a mailbox's Microsoft sign-in, against a real (SQLite) database.</summary>
public sealed class MailboxConfigRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager Mona", UserRole.TechnicalManager);
    private readonly AppUser coordinator;

    public MailboxConfigRepositoryTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        coordinator = AppUser.Create("ahmed@example.com", "Ahmed", UserRole.Coordinator, managerId: manager.Id);
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Users.AddRange(manager, coordinator);
        db.SaveChanges();
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

    private void Store(CoordinatorEmailConfig config)
    {
        using var db = CreateContext();
        db.CoordinatorEmailConfigs.Add(config);
        db.SaveChanges();
    }

    private CoordinatorEmailConfig Read()
    {
        using var db = CreateContext();
        return new EfCoordinatorEmailConfigRepository(db).GetByCoordinatorNoTrackingAsync(coordinator.Id).GetAwaiter().GetResult()!;
    }

    [Fact]
    public async Task RotatingTheRefreshTokenOnlyWorksFromTheTokenThatWasUsed()
    {
        Store(CoordinatorEmailConfig.CreateMicrosoft(coordinator.Id, manager.Id, "ahmed@example.com", "E1", "tenant-1"));
        await using var db = CreateContext();
        var repository = new EfCoordinatorEmailConfigRepository(db);

        Assert.True(await repository.RotateRefreshTokenAsync(coordinator.Id, "E1", "E2"));
        Assert.False(await repository.RotateRefreshTokenAsync(coordinator.Id, "E1", "E3"));

        Assert.Equal("E2", Read().EncryptedRefreshToken);
    }

    [Fact]
    public async Task MarkingNeedsReconnectOnlyWorksFromTheTokenThatWasRefused()
    {
        Store(CoordinatorEmailConfig.CreateMicrosoft(coordinator.Id, manager.Id, "ahmed@example.com", "E1", "tenant-1"));
        await using var db = CreateContext();
        var repository = new EfCoordinatorEmailConfigRepository(db);

        Assert.False(await repository.MarkNeedsReconnectAsync(coordinator.Id, "someone-elses-token", "revoked"));
        Assert.Equal(MailboxConnectionStatus.Connected, Read().ConnectionStatus);

        Assert.True(await repository.MarkNeedsReconnectAsync(coordinator.Id, "E1", "revoked"));
        var config = Read();
        Assert.Equal(MailboxConnectionStatus.NeedsReconnect, config.ConnectionStatus);
        Assert.Equal("revoked", config.LastAuthError);
    }

    [Fact]
    public async Task APasswordMailboxIsNeverMarkedNeedsReconnect()
    {
        Store(CoordinatorEmailConfig.Create(coordinator.Id, manager.Id, "imap.gmail.com", 993, "ahmed@gmail.com", "enc-password", true));
        await using var db = CreateContext();

        var marked = await new EfCoordinatorEmailConfigRepository(db).MarkNeedsReconnectAsync(coordinator.Id, "enc-password", "revoked");

        Assert.False(marked);
        Assert.Equal(MailboxConnectionStatus.Connected, Read().ConnectionStatus);
    }

    public void Dispose() => connection.Dispose();
}
