using Inspecta.Application.Services;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
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

    [Fact]
    public async Task QuotationDraftListCanBeScopedToTenantOrCoordinator()
    {
        var repository = new RecordingQuotationDraftRepository();
        var service = new QuotationDraftService(repository);
        var tenantId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();

        await service.ListAsync(tenantId, null);
        Assert.Equal(tenantId, repository.LastTenantId);
        Assert.Null(repository.LastCoordinatorId);

        await service.ListAsync(tenantId, coordinatorId);
        Assert.Equal(tenantId, repository.LastTenantId);
        Assert.Equal(coordinatorId, repository.LastCoordinatorId);

        await service.ListAsync(null, null);
        Assert.Null(repository.LastTenantId);
        Assert.Null(repository.LastCoordinatorId);
    }

    [Fact]
    public void IncomingEmailCreatesEmptyDraftAndCoordinatorCanDecideIt()
    {
        var draft = QuotationDraft.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            clientName: null,
            location: null,
            machineDetails: null,
            rawEmailBody: "Please send us a quotation.",
            emailHtmlBody: "<p>Please send us a quotation.</p>",
            senderEmail: "client@example.com",
            senderName: "Client",
            emailSubject: "Quotation request",
            toAddresses: [new EmailAddressInfo("coordinator@example.com", "Coordinator")],
            ccAddresses: [new EmailAddressInfo("copy@example.com", null)],
            bccAddresses: null,
            replyToAddresses: null,
            attachments: [new EmailAttachmentInfo("machine.pdf", "application/pdf", 128)],
            emailMessageId: "<message-1@example.com>",
            inReplyTo: null,
            references: null,
            emailDate: DateTimeOffset.UtcNow,
            sourceUidValidity: "42",
            sourceUid: 5);

        Assert.Null(draft.ClientName);
        Assert.Null(draft.Location);
        Assert.Null(draft.MachineDetails);
        Assert.Equal(QuotationDraftStatus.Draft, draft.Status);
        Assert.Single(draft.GetToAddresses());
        Assert.Single(draft.GetCcAddresses());
        Assert.Equal("machine.pdf", Assert.Single(draft.GetAttachments()).FileName);
        Assert.Null(draft.AmountPiastres);
        Assert.Null(draft.ValidUntil);
        Assert.Throws<InvalidOperationException>(() => draft.SetDecision(QuotationDraftStatus.CounterOffered));

        draft.UpdateDetails("Acme", "Cairo", "Machine X", 250_000, DateTimeOffset.UtcNow.AddDays(30));
        draft.SetDecision(QuotationDraftStatus.CounterOffered);
        Assert.Equal(QuotationDraftStatus.CounterOffered, draft.Status);

        draft.SetDecision(QuotationDraftStatus.Accepted);
        Assert.Equal(QuotationDraftStatus.Accepted, draft.Status);
        Assert.Throws<InvalidOperationException>(() => draft.SetDecision(QuotationDraftStatus.Rejected));
    }

    private sealed class RecordingQuotationDraftRepository : IQuotationDraftRepository
    {
        public Guid? LastTenantId { get; private set; }
        public Guid? LastCoordinatorId { get; private set; }

        public Task<IReadOnlyList<QuotationDraft>> ListAsync(
            Guid? tenantId,
            Guid? coordinatorId,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastCoordinatorId = coordinatorId;
            return Task.FromResult<IReadOnlyList<QuotationDraft>>([]);
        }

        public Task<QuotationDraft?> GetForCoordinatorAsync(
            Guid coordinatorId,
            Guid draftId,
            bool tracking,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
