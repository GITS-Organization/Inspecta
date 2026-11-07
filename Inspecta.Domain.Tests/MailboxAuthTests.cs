using System.Security.Cryptography;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Inspecta.Domain.Tests;

public sealed class MailboxAuthTests
{
    private static readonly Guid CoordinatorId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();

    private sealed class FakeOAuth : IMicrosoftOAuthClient
    {
        public int RefreshCalls { get; private set; }
        public MicrosoftTokens Next { get; set; } = new("at-1", null, DateTimeOffset.UtcNow.AddHours(1), string.Empty, string.Empty);
        public bool Reject { get; set; }
        public bool IsConfigured => true;
        public string BuildAuthorizationUrl(string state, string codeChallenge) => "https://login.example.test/?state=" + state;
        public Task<MicrosoftTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MicrosoftTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            if (Reject) throw new MicrosoftTokenRejectedException("rejected");
            return Task.FromResult(Next);
        }
    }

    private sealed class MemoryConfigs(CoordinatorEmailConfig config) : ICoordinatorEmailConfigRepository
    {
        public int Saves { get; private set; }
        public Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(Guid coordinatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CoordinatorEmailConfig?>(config.CoordinatorId == coordinatorId ? config : null);

        public Task SaveAsync(CoordinatorEmailConfig saved, CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ISecretProtector Protector() => new AesGcmSecretProtector(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["IMAP_ENCRYPTION_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) })
        .Build());

    private static (MailboxAuthenticator Authenticator, FakeOAuth OAuth, MemoryConfigs Configs, ISecretProtector Protector, ManualClock Clock) Setup(CoordinatorEmailConfig config, ISecretProtector? protector = null)
    {
        protector ??= Protector();
        var oauth = new FakeOAuth();
        var configs = new MemoryConfigs(config);
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        return (new MailboxAuthenticator(protector, oauth, configs, new MailboxTokenCache(), clock), oauth, configs, protector, clock);
    }

    private static CoordinatorEmailConfig Microsoft(DateTimeOffset? syncFrom = null) =>
        CoordinatorEmailConfig.CreateMicrosoft(CoordinatorId, ManagerId, "ahmed@integrity.example", "enc-refresh", "tenant-1", syncFrom);

    [Fact]
    public void CreateMicrosoftUsesOutlookServersAndIsConnected()
    {
        var config = Microsoft();

        Assert.Equal("outlook.office365.com", config.ImapServer);
        Assert.Equal(993, config.ImapPort);
        Assert.True(config.UseSsl);
        var smtp = config.ResolveSmtp();
        Assert.Equal(new SmtpEndpoint("smtp.office365.com", 587, false), smtp);
        Assert.Equal(MailboxAuthMode.MicrosoftOAuth, config.AuthMode);
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Equal("ahmed@integrity.example", config.EmailUsername);
        Assert.Equal("enc-refresh", config.EncryptedRefreshToken);
        Assert.Equal("tenant-1", config.MicrosoftTenantId);
        Assert.Equal(string.Empty, config.EncryptedPassword);
    }

    [Fact]
    public void MarkNeedsReconnectKeepsTheTokenAndRecordsTheReason()
    {
        var config = Microsoft();

        config.MarkNeedsReconnect(new string('x', 600));

        Assert.Equal(MailboxConnectionStatus.NeedsReconnect, config.ConnectionStatus);
        Assert.Equal(500, config.LastAuthError!.Length);
        Assert.Equal("enc-refresh", config.EncryptedRefreshToken);
    }

    [Fact]
    public void ConnectMicrosoftAgainClearsTheErrorAndRestoresConnected()
    {
        var config = Microsoft();
        config.MarkNeedsReconnect("revoked");

        config.ConnectMicrosoft("ahmed@integrity.example", "enc-refresh-2", "tenant-1");

        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
        Assert.Equal("enc-refresh-2", config.EncryptedRefreshToken);
    }

    [Fact]
    public void ConnectingADifferentMailboxStartsTheImportAgain()
    {
        var old = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var config = Microsoft(old);

        config.ConnectMicrosoft("someone.else@integrity.example", "enc-refresh-3", "tenant-1");

        Assert.True(config.SyncFromUtc > old);
        Assert.Null(config.LastUidValidity);
        Assert.Equal(0, config.LastUid);
    }

    [Fact]
    public async Task PasswordModeReturnsTheDecryptedPasswordAndNoToken()
    {
        var protector = Protector();
        var config = CoordinatorEmailConfig.Create(CoordinatorId, ManagerId, "imap.gmail.com", 993, "x@gmail.com", protector.Protect("app-pass"), true);
        var (authenticator, oauth, _, _, _) = Setup(config, protector);

        var credential = await authenticator.GetAsync(config);

        Assert.Equal(new MailboxCredential("x@gmail.com", "app-pass", null), credential);
        Assert.Equal(0, oauth.RefreshCalls);
    }

    [Fact]
    public async Task MicrosoftModeRefreshesOnceAndCachesTheAccessToken()
    {
        var protector = Protector();
        var config = CoordinatorEmailConfig.CreateMicrosoft(CoordinatorId, ManagerId, "a@integrity.example", protector.Protect("rt-1"), "tenant-1");
        var (authenticator, oauth, _, _, _) = Setup(config, protector);

        var first = await authenticator.GetAsync(config);
        var second = await authenticator.GetAsync(config);

        Assert.Equal(new MailboxCredential("a@integrity.example", null, "at-1"), first);
        Assert.Equal("at-1", second.AccessToken);
        Assert.Equal(1, oauth.RefreshCalls);
    }

    [Fact]
    public async Task AnAccessTokenWithLessThanTwoMinutesLeftIsRenewed()
    {
        var protector = Protector();
        var config = CoordinatorEmailConfig.CreateMicrosoft(CoordinatorId, ManagerId, "a@integrity.example", protector.Protect("rt-1"), "tenant-1");
        var (authenticator, oauth, _, _, clock) = Setup(config, protector);
        oauth.Next = new MicrosoftTokens("at-1", null, clock.Now.AddMinutes(10), string.Empty, string.Empty);

        await authenticator.GetAsync(config);
        clock.Now = clock.Now.AddMinutes(8).AddSeconds(30);
        await authenticator.GetAsync(config);

        Assert.Equal(2, oauth.RefreshCalls);
    }

    [Fact]
    public async Task ARotatedRefreshTokenIsSavedEncrypted()
    {
        var protector = Protector();
        var config = CoordinatorEmailConfig.CreateMicrosoft(CoordinatorId, ManagerId, "a@integrity.example", protector.Protect("rt-1"), "tenant-1");
        var (authenticator, oauth, configs, _, _) = Setup(config, protector);
        oauth.Next = new MicrosoftTokens("at-2", "rt-2", DateTimeOffset.UtcNow.AddHours(1), string.Empty, string.Empty);

        await authenticator.GetAsync(config);

        Assert.Equal(1, configs.Saves);
        Assert.NotEqual("rt-2", config.EncryptedRefreshToken);
        Assert.Equal("rt-2", protector.Unprotect(config.EncryptedRefreshToken!));
    }

    [Fact]
    public async Task InvalidGrantMarksNeedsReconnectAndThrowsMailboxReconnectRequired()
    {
        var protector = Protector();
        var config = CoordinatorEmailConfig.CreateMicrosoft(CoordinatorId, ManagerId, "a@integrity.example", protector.Protect("rt-1"), "tenant-1");
        var (authenticator, oauth, configs, _, _) = Setup(config, protector);
        oauth.Reject = true;

        var exception = await Assert.ThrowsAsync<AuthException>(() => authenticator.GetAsync(config));

        Assert.Equal("mailbox_reconnect_required", exception.Code);
        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(MailboxConnectionStatus.NeedsReconnect, config.ConnectionStatus);
        Assert.Equal(1, configs.Saves);
        Assert.DoesNotContain("rt-1", exception.Message);
    }

    [Fact]
    public async Task AMailboxAlreadyNeedingReconnectThrowsWithoutCallingMicrosoft()
    {
        var protector = Protector();
        var config = CoordinatorEmailConfig.CreateMicrosoft(CoordinatorId, ManagerId, "a@integrity.example", protector.Protect("rt-1"), "tenant-1");
        config.MarkNeedsReconnect("revoked");
        var (authenticator, oauth, _, _, _) = Setup(config, protector);

        var exception = await Assert.ThrowsAsync<AuthException>(() => authenticator.GetAsync(config));

        Assert.Equal("mailbox_reconnect_required", exception.Code);
        Assert.Equal(0, oauth.RefreshCalls);
    }

    [Fact]
    public void SettingAPasswordSwitchesBackToPasswordMode()
    {
        var config = Microsoft();
        config.MarkNeedsReconnect("revoked");

        config.Update("imap.gmail.com", 993, "someone@gmail.com", "enc-password", true);

        Assert.Equal(MailboxAuthMode.Password, config.AuthMode);
        Assert.Null(config.EncryptedRefreshToken);
        Assert.Null(config.MicrosoftTenantId);
        Assert.Equal(MailboxConnectionStatus.Connected, config.ConnectionStatus);
        Assert.Null(config.LastAuthError);
        Assert.Equal("enc-password", config.EncryptedPassword);
    }
}
