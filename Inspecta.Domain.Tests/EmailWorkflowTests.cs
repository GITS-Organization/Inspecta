using Inspecta.Application.Services;
using Inspecta.Application.Exceptions;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

public sealed class EmailWorkflowTests
{
    [Fact]
    public void ParserExtractsClientLocationAndMachineDetailsFromLabeledEmail()
    {
        var parser = new EmailParserService();

        var parsed = parser.Parse(
            """
            Hello,
            Client: Acme Industries
            Location: Cairo
            Machine model: X-100
            Serial number: SN-42
            Issue description: The motor vibrates under load.
            """);

        Assert.Equal("Acme Industries", parsed.ClientName);
        Assert.Equal("Cairo", parsed.Location);
        Assert.Contains("Model: X-100", parsed.MachineDetails);
        Assert.Contains("Serial: SN-42", parsed.MachineDetails);
        Assert.Contains("Issue: The motor vibrates under load.", parsed.MachineDetails);
    }

    [Fact]
    public void ParserLeavesMissingFieldsNullAndReadsCommonNaturalLanguage()
    {
        var parser = new EmailParserService();

        var parsed = parser.Parse("Hi, my name is Samir and our equipment is located in Giza.");

        Assert.Equal("Samir", parsed.ClientName);
        Assert.Equal("Giza", parsed.Location);
        Assert.Null(parsed.MachineDetails);
    }

    [Fact]
    public void SecretProtectorRoundTripsAndAuthenticatesEncryptedSecrets()
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["IMAP_ENCRYPTION_KEY"] = key })
            .Build();
        var protector = new AesGcmSecretProtector(configuration);

        var encrypted = protector.Protect("private-mail-password");
        var altered = encrypted[..^2] + (encrypted[^2] == 'A' ? "B" : "A") + encrypted[^1];

        Assert.NotEqual("private-mail-password", encrypted);
        Assert.Equal("private-mail-password", protector.Unprotect(encrypted));
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => protector.Unprotect(altered));
    }

    [Fact]
    public void MissingEncryptionKeyProducesConfigurationErrorInsteadOfConflictException()
    {
        var protector = new AesGcmSecretProtector(new ConfigurationBuilder().Build());

        Assert.Throws<EmailEncryptionConfigurationException>(() => protector.Protect("mail-password"));
    }

    [Fact]
    public void MailboxUsernameHasNoGlobalUniqueConstraintButCoordinatorDoesHaveOneConfig()
    {
        var options = new DbContextOptionsBuilder<InspectaDbContext>()
            .UseNpgsql("Host=localhost;Database=inspecta_model_test;Username=postgres")
            .Options;
        using var dbContext = new InspectaDbContext(options, new CurrentTenant(new HttpContextAccessor()));
        var configEntity = dbContext.Model.FindEntityType(typeof(CoordinatorEmailConfig))!;
        var usernameIndex = configEntity.GetIndexes()
            .SingleOrDefault(index => index.Properties.Any(property =>
                property.Name == nameof(CoordinatorEmailConfig.EmailUsername)));

        Assert.Null(usernameIndex);
        Assert.Contains(
            configEntity.GetKeys(),
            key => key.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CoordinatorEmailConfig.CoordinatorId)]));
    }
}
