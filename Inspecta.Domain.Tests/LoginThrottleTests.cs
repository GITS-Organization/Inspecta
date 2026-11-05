using System.Security.Claims;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Inspecta.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>Guessing a password is slowed down: five failures lock the email, and a success starts again.</summary>
public sealed class LoginThrottleTests : IDisposable
{
    private const string Password = "Correct-Horse-Battery-1";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly ManualClock clock = new();

    public LoginThrottleTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
        var user = AppUser.Create("staff@example.com", "Staff Member", UserRole.Admin);
        user.SetPasswordHash(new IdentityPasswordService().Hash(user, Password));
        db.Users.Add(user);
        db.SaveChanges();
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private InspectaDbContext CreateContext() => new(
        options,
        new CurrentTenant(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
        }));

    private sealed class FixedTokens : IAccessTokenService
    {
        public (string Token, DateTimeOffset ExpiresAt) Create(AppUser user) => ("token", DateTimeOffset.UtcNow.AddMinutes(10));
    }

    private async Task<AuthResult> Login(LoginAttemptTracker tracker, string email, string password)
    {
        await using var db = CreateContext();
        var service = new AuthService(new EfUserRepository(db), new IdentityPasswordService(), new FixedTokens(), tracker);
        return await service.LoginAsync(new LoginRequest { Email = email, Password = password });
    }

    [Fact]
    public async Task FiveFailuresLockTheEmailForFifteenMinutesEvenForTheRightPassword()
    {
        var tracker = new LoginAttemptTracker(clock);
        for (var attempt = 0; attempt < LoginAttemptTracker.MaxFailures; attempt++)
        {
            var wrong = await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "staff@example.com", "wrong-" + attempt));
            Assert.Equal("invalid_credentials", wrong.Code);
        }

        var locked = await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "STAFF@example.com", Password));
        Assert.Equal("too_many_attempts", locked.Code);
        Assert.Equal(429, locked.StatusCode);

        clock.Now = clock.Now.AddMinutes(14);
        Assert.Equal("too_many_attempts", (await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "staff@example.com", Password))).Code);
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Equal("token", (await Login(tracker, "staff@example.com", Password)).AccessToken);
    }

    [Fact]
    public async Task ASuccessClearsTheCountAndAnUnknownEmailIsCountedLikeAKnownOne()
    {
        var tracker = new LoginAttemptTracker(clock);
        for (var attempt = 0; attempt < LoginAttemptTracker.MaxFailures - 1; attempt++)
            await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "staff@example.com", "wrong"));
        Assert.Equal("token", (await Login(tracker, "staff@example.com", Password)).AccessToken); // success: the count starts again
        for (var attempt = 0; attempt < LoginAttemptTracker.MaxFailures - 1; attempt++)
            await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "staff@example.com", "wrong"));
        Assert.Equal("token", (await Login(tracker, "staff@example.com", Password)).AccessToken);

        // Nobody has this email, and it is locked all the same: the answer does not say whether an account exists.
        for (var attempt = 0; attempt < LoginAttemptTracker.MaxFailures; attempt++)
            Assert.Equal("invalid_credentials", (await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "nobody@example.com", "x"))).Code);
        Assert.Equal("too_many_attempts", (await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "nobody@example.com", "x"))).Code);
        // Another email is not affected.
        Assert.Equal("token", (await Login(tracker, "staff@example.com", Password)).AccessToken);
    }

    [Fact]
    public async Task FailuresOlderThanTheWindowAreForgotten()
    {
        var tracker = new LoginAttemptTracker(clock);
        for (var attempt = 0; attempt < LoginAttemptTracker.MaxFailures - 1; attempt++)
            await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "staff@example.com", "wrong"));
        clock.Now = clock.Now.AddMinutes(16);
        await Assert.ThrowsAsync<AuthException>(() => Login(tracker, "staff@example.com", "wrong")); // would be the fifth, but the window moved on
        Assert.Equal("token", (await Login(tracker, "staff@example.com", Password)).AccessToken);
    }

    public void Dispose() => connection.Dispose();
}
