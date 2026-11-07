using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class MailboxAuthTests
{
    private static readonly Guid CoordinatorId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();

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
