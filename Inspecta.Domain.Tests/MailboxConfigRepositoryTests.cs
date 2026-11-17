using System.Security.Claims;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private CoordinatorEmailConfig StoreMailbox(Action<CoordinatorEmailConfig>? change = null)
    {
        var config = CoordinatorEmailConfig.Create(coordinator.Id, manager.Id, "imap.gmail.com", 993, "ahmed@gmail.com", "enc-password", true);
        change?.Invoke(config);
        Store(config);
        return config;
    }

    [Fact]
    public async Task ACheckThatPassedIsRecordedAndClearsAnOldProblem()
    {
        StoreMailbox(config => config.RecordFailedCheck(At.AddHours(-1), "The mailbox refused the email or password.", "sign_in"));
        await using var db = CreateContext();

        await new EfCoordinatorEmailConfigRepository(db).RecordCheckAsync(coordinator.Id, Read().UpdatedAt, At, failure: null, step: null);

        var stored = Read();
        Assert.Equal(At, stored.LastCheckedAt);
        Assert.Equal(MailboxConnectionStatus.Connected, stored.ConnectionStatus);
        Assert.Null(stored.LastAuthError);
    }

    [Fact]
    public async Task ACheckThatFailedLeavesAProblemWithItsReasonAndStep()
    {
        StoreMailbox();
        await using var db = CreateContext();

        await new EfCoordinatorEmailConfigRepository(db).RecordCheckAsync(coordinator.Id, Read().UpdatedAt, At, "The mailbox refused the email or password.", "sign_in");

        var stored = Read();
        Assert.Equal(MailboxConnectionStatus.Problem, stored.ConnectionStatus);
        Assert.Equal("The mailbox refused the email or password.", stored.LastAuthError);
        Assert.Equal("sign_in", stored.LastFailedStep);
        Assert.Equal(At, stored.LastCheckedAt);
    }

    [Fact]
    public async Task ARecordingMadeAboutOldSettingsIsDroppedWhenTheSettingsChangedMeanwhile()
    {
        StoreMailbox();
        var settingsItWasTriedWith = Read().UpdatedAt.AddMinutes(-5);
        await using var db = CreateContext();

        await new EfCoordinatorEmailConfigRepository(db).RecordCheckAsync(coordinator.Id, settingsItWasTriedWith, At, "The mailbox refused the email or password.", "sign_in");
        await new EfCoordinatorEmailConfigRepository(db).RecordSyncAsync(coordinator.Id, settingsItWasTriedWith, At, "We could not reach the server.", "reach");

        var stored = Read();
        Assert.Equal(MailboxConnectionStatus.Connected, stored.ConnectionStatus);
        Assert.Null(stored.LastCheckedAt);
    }

    [Fact]
    public async Task RecordingForAMailboxThatWasRemovedMeanwhileDoesNothing()
    {
        await using var db = CreateContext();
        var repository = new EfCoordinatorEmailConfigRepository(db);

        await repository.RecordCheckAsync(coordinator.Id, At, At, failure: null, step: null);
        await repository.RecordSyncAsync(coordinator.Id, At, At, failure: null, step: null);
        await repository.RecordEmailReceivedAsync(coordinator.Id, At);

        Assert.Null(await repository.GetByCoordinatorNoTrackingAsync(coordinator.Id));
    }

    [Fact]
    public async Task ASyncThatSignedInDoesNotEndASendingProblemAndAStillHealthyMailboxIsNotRewrittenEveryCycle()
    {
        StoreMailbox(config => config.RecordFailedCheck(At.AddHours(-1), "We can read the mailbox but cannot send from it.", "send"));
        await using var db = CreateContext();
        var repository = new EfCoordinatorEmailConfigRepository(db);

        await repository.RecordSyncAsync(coordinator.Id, Read().UpdatedAt, At, failure: null, step: null);

        var stored = Read();
        Assert.Equal(MailboxConnectionStatus.Problem, stored.ConnectionStatus);
        Assert.Equal("send", stored.LastFailedStep);
    }

    [Fact]
    public async Task TheSameSyncNewsIsNotWrittenAgainWithinTheInterval()
    {
        StoreMailbox();
        await using var db = CreateContext();
        var repository = new EfCoordinatorEmailConfigRepository(db);
        var settings = Read().UpdatedAt;

        await repository.RecordSyncAsync(coordinator.Id, settings, At, failure: null, step: null);
        await repository.RecordSyncAsync(coordinator.Id, settings, At.AddMinutes(2), failure: null, step: null);

        Assert.Equal(At, Read().LastCheckedAt);
    }

    [Fact]
    public async Task TheLastEmailIsStoredAndOnlyMovesForward()
    {
        StoreMailbox();
        await using var db = CreateContext();
        var repository = new EfCoordinatorEmailConfigRepository(db);

        await repository.RecordEmailReceivedAsync(coordinator.Id, At);
        await repository.RecordEmailReceivedAsync(coordinator.Id, At.AddDays(-2));

        Assert.Equal(At, Read().LastEmailAt);
    }

    [Fact]
    public void TheColumnForTheEncryptedRefreshTokenFitsTheLongestTokenMicrosoftGivesOut()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var protector = new Inspecta.Infrastructure.Services.AesGcmSecretProtector(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["IMAP_ENCRYPTION_KEY"] = key }).Build());
        var needed = protector.Protect(new string('r', 4000)).Length;
        using var db = CreateContext();
        var column = db.Model.FindEntityType(typeof(CoordinatorEmailConfig))!.FindProperty(nameof(CoordinatorEmailConfig.EncryptedRefreshToken))!.GetMaxLength();

        Assert.True(column is null || column >= needed, $"the column holds {column} characters but an encrypted 4000-character refresh token needs {needed}");
    }

    public void Dispose() => connection.Dispose();
}
