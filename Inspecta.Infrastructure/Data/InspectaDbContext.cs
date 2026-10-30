using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Inspecta.Infrastructure.Data;

public sealed class InspectaDbContext(
    DbContextOptions<InspectaDbContext> options,
    CurrentTenant currentTenant) : DbContext(options)
{
    private bool IsTenantAdmin => currentTenant.IsAdmin;
    private Guid? TenantId => currentTenant.TenantId;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<InspectionRequest> Requests => Set<InspectionRequest>();
    public DbSet<RequestItem> RequestItems => Set<RequestItem>();
    public DbSet<RequestEmail> RequestEmails => Set<RequestEmail>();
    public DbSet<ClientMailRule> ClientMailRules => Set<ClientMailRule>();
    public DbSet<OutboundEmail> OutboundEmails => Set<OutboundEmail>();
    public DbSet<JobStatusHistory> JobStatusHistory => Set<JobStatusHistory>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<InspectionCertificate> Certificates => Set<InspectionCertificate>();
    public DbSet<CoordinatorEmailConfig> CoordinatorEmailConfigs => Set<CoordinatorEmailConfig>();
    public DbSet<QuotationDraft> QuotationDrafts => Set<QuotationDraft>();
    public DbSet<CertificateCounter> CertificateCounters => Set<CertificateCounter>();
    public DbSet<IssuedCertificateFile> IssuedCertificateFiles => Set<IssuedCertificateFile>();
    public DbSet<CertificateDelivery> CertificateDeliveries => Set<CertificateDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.HasKey(item => item.Id);
        user.HasIndex(item => new { item.ManagerId, item.Role });
        user.HasQueryFilter(item => !item.IsDeleted);
        user.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
        user.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        user.Property(item => item.Email).HasMaxLength(320).IsRequired();
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.FullName).HasMaxLength(200).IsRequired();
        user.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(item => item.Role).HasConversion<string>().HasMaxLength(32);
        user.Property(item => item.QualificationsJson).HasMaxLength(4000).IsRequired();
        user.Property(item => item.SubscriptionStatus).HasConversion<string>().HasMaxLength(24);

        var refreshToken = modelBuilder.Entity<RefreshToken>();
        refreshToken.HasKey(item => item.Id);
        refreshToken.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
        refreshToken.HasIndex(item => item.TokenHash).IsUnique();
        refreshToken.HasIndex(item => new { item.UserId, item.FamilyId });
        refreshToken.HasQueryFilter(item => item.User != null && !item.User.IsDeleted);
        refreshToken.HasOne(item => item.User)
            .WithMany(item => item.RefreshTokens)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var client = modelBuilder.Entity<Client>();
        client.HasKey(item => item.Id);
        client.HasIndex(item => item.ManagerId);
        client.HasQueryFilter(item => IsTenantAdmin || TenantId != null && item.ManagerId == TenantId);
        client.HasOne<AppUser>().WithMany().HasForeignKey(item => item.ManagerId).OnDelete(DeleteBehavior.Restrict);
        client.Property(item => item.Name).HasMaxLength(200).IsRequired();
        client.HasMany(item => item.Contacts).WithOne().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Cascade);
        client.HasMany(item => item.Requests).WithOne(item => item.Client)
            .HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Restrict);

        var contact = modelBuilder.Entity<ClientContact>();
        contact.HasKey(item => item.Id);
        contact.HasQueryFilter(value => IsTenantAdmin ||
            TenantId != null && value.Client != null && value.Client.ManagerId == TenantId);
        contact.Property(item => item.Name).HasMaxLength(200).IsRequired();
        contact.Property(item => item.Email).HasMaxLength(320);
        contact.HasOne(value => value.Client).WithMany(value => value.Contacts)
            .HasForeignKey(value => value.ClientId).OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<Item>();
        item.ToTable("Items");
        item.HasKey(item => item.Id);
        item.Property(item => item.Name).HasMaxLength(200).IsRequired();
        item.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(24);
        item.Property(item => item.InspectionStandard).HasMaxLength(200);
        item.Property(item => item.TestType).HasMaxLength(200);
        item.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        item.Property(item => item.LastInspectionResult).HasConversion<string>().HasMaxLength(32);
        item.Property(item => item.LabelsJson).IsRequired();
        item.Property(item => item.DatesJson).IsRequired();
        item.Property(item => item.ChecklistJson).IsRequired();
        item.Property(item => item.AttachmentsJson).IsRequired();
        item.HasQueryFilter(value => IsTenantAdmin ||
            TenantId != null && value.Client != null && value.Client.ManagerId == TenantId);
        item.HasOne(value => value.Client).WithMany().HasForeignKey(value => value.ClientId).OnDelete(DeleteBehavior.Restrict);

        var request = modelBuilder.Entity<InspectionRequest>();
        request.HasKey(item => item.Id);
        request.HasIndex(item => item.ManagerId);
        request.HasIndex(item => item.CoordinatorId);
        request.Property(item => item.EmailFromName).HasMaxLength(500);
        request.HasOne(item => item.Email).WithOne(item => item.Request)
            .HasForeignKey<RequestEmail>(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
        request.HasQueryFilter(item => IsTenantAdmin || TenantId != null && item.ManagerId == TenantId);
        request.HasOne<AppUser>().WithMany().HasForeignKey(item => item.ManagerId).OnDelete(DeleteBehavior.Restrict);
        request.Property(item => item.ClientName).HasMaxLength(200).IsRequired();
        request.Property(item => item.Location).HasMaxLength(500).IsRequired();
        request.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        request.Property(item => item.Source).HasConversion<string>().HasMaxLength(24);
        request.Property(item => item.Triage).HasConversion<string>().HasMaxLength(16);
        request.HasIndex(item => new { item.Triage, item.CreatedAt });
        request.HasMany(item => item.Items).WithOne(item => item.Request).HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
        request.HasMany(item => item.Certificates).WithOne(item => item.Request)
            .HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
        request.HasMany(item => item.History).WithOne(item => item.Request).HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
        request.HasOne(item => item.Client).WithMany(item => item.Requests)
            .HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Restrict);

        var requestItem = modelBuilder.Entity<RequestItem>();
        requestItem.HasKey(item => item.Id);
        requestItem.HasQueryFilter(value => IsTenantAdmin ||
            TenantId != null && value.Request != null && value.Request.ManagerId == TenantId);
        requestItem.Property(item => item.Name).HasMaxLength(200).IsRequired();
        requestItem.HasOne(value => value.Request).WithMany(value => value.Items)
            .HasForeignKey(value => value.RequestId).OnDelete(DeleteBehavior.Cascade);
        requestItem.HasOne<Item>().WithMany().HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.SetNull);
        requestItem.HasMany(item => item.Certificates).WithOne(item => item.RequestItem)
            .HasForeignKey(item => item.RequestItemId).OnDelete(DeleteBehavior.Restrict);

        var requestEmail = modelBuilder.Entity<RequestEmail>();
        requestEmail.HasKey(item => item.Id);
        requestEmail.HasIndex(item => item.RequestId).IsUnique();
        requestEmail.HasIndex(item => new { item.CoordinatorId, item.MailboxUidValidity, item.MailboxUid }).IsUnique();
        requestEmail.HasQueryFilter(value => IsTenantAdmin ||
            TenantId != null && value.Request != null && value.Request.ManagerId == TenantId);
        requestEmail.HasOne<AppUser>().WithMany().HasForeignKey(item => item.CoordinatorId).OnDelete(DeleteBehavior.Restrict);
        requestEmail.Property(item => item.MailboxUidValidity).HasMaxLength(64).IsRequired();
        requestEmail.Property(item => item.MessageId).HasMaxLength(998);
        requestEmail.Property(item => item.InReplyTo).HasMaxLength(998);
        requestEmail.Property(item => item.FromAddress).HasMaxLength(320);
        requestEmail.Property(item => item.FromName).HasMaxLength(500);
        requestEmail.Property(item => item.Subject).HasMaxLength(998);

        var history = modelBuilder.Entity<JobStatusHistory>();
        history.HasKey(item => item.Id);
        history.HasQueryFilter(value => IsTenantAdmin ||
            TenantId != null && value.Request != null && value.Request.ManagerId == TenantId);
        history.Property(item => item.FromStatus).HasConversion<string>();
        history.Property(item => item.ToStatus).HasConversion<string>();
        history.HasIndex(item => new { item.RequestId, item.OccurredAt });
        history.HasOne(value => value.Request).WithMany(value => value.History)
            .HasForeignKey(value => value.RequestId).OnDelete(DeleteBehavior.Cascade);

        var quote = modelBuilder.Entity<Quote>();
        quote.HasKey(item => item.Id);
        quote.HasQueryFilter(item => IsTenantAdmin ||
            TenantId != null && item.Request != null && item.Request.ManagerId == TenantId);
        quote.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        quote.Property(item => item.Type).HasConversion<string>().HasMaxLength(24);
        quote.Property(item => item.ContactName).HasMaxLength(200);
        quote.Property(item => item.QuotationNumber).HasMaxLength(40);
        quote.HasIndex(item => new { item.RequestId, item.Version }).IsUnique();
        quote.HasOne(item => item.Request).WithMany().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);

        var certificate = modelBuilder.Entity<InspectionCertificate>();
        certificate.HasKey(item => item.Id);
        certificate.HasQueryFilter(item => IsTenantAdmin ||
            TenantId != null && item.Request != null && item.Request.ManagerId == TenantId);
        certificate.HasIndex(item => new { item.RequestId, item.RequestItemId }).IsUnique();
        certificate.Property(item => item.FunctionCheck).HasConversion<string>().HasMaxLength(24);
        certificate.Property(item => item.FinalResult).HasConversion<string>().HasMaxLength(32);
        certificate.HasIndex(item => item.VerificationCode);
        certificate.HasIndex(item => item.CertificateNumber);

        var counter = modelBuilder.Entity<CertificateCounter>();
        counter.HasKey(item => item.Id);
        counter.HasIndex(item => new { item.ManagerId, item.Year }).IsUnique();
        // Two approvals taking the same number: the second save fails and takes the next one.
        counter.Property(item => item.LastNumber).IsConcurrencyToken();

        var issued = modelBuilder.Entity<IssuedCertificateFile>();
        issued.HasKey(item => item.Id);
        issued.HasQueryFilter(item => IsTenantAdmin || TenantId != null && item.ManagerId == TenantId);
        issued.HasIndex(item => item.CertificateId).IsUnique();
        issued.HasIndex(item => item.VerificationCode);
        issued.HasIndex(item => new { item.ClientId, item.IssuedAt });
        issued.HasIndex(item => item.EquipmentId);
        issued.Property(item => item.ClientName).HasMaxLength(200);
        issued.Property(item => item.ItemName).HasMaxLength(200);
        issued.Property(item => item.CertificateNumber).HasMaxLength(64);
        issued.Property(item => item.VerificationCode).HasMaxLength(64);
        issued.Property(item => item.QrData).HasMaxLength(512);
        issued.Property(item => item.PreparedBy).HasMaxLength(200);
        issued.Property(item => item.FileName).HasMaxLength(260);
        issued.Property(item => item.Sha256).HasMaxLength(64);

        var delivery = modelBuilder.Entity<CertificateDelivery>();
        delivery.HasKey(item => item.Id);
        delivery.HasQueryFilter(item => IsTenantAdmin || TenantId != null && item.ManagerId == TenantId);
        delivery.HasIndex(item => item.RequestId);
        delivery.Property(item => item.ToAddress).HasMaxLength(320);
        delivery.Property(item => item.Subject).HasMaxLength(500);
        delivery.Property(item => item.MessageId).HasMaxLength(255);
        delivery.Property(item => item.AttachmentNames).HasMaxLength(2000);

        var emailConfig = modelBuilder.Entity<CoordinatorEmailConfig>();
        emailConfig.HasKey(item => item.CoordinatorId);
        emailConfig.HasIndex(item => item.TenantId);
        emailConfig.Property(item => item.ImapServer).HasMaxLength(255).IsRequired();
        emailConfig.Property(item => item.EmailUsername).HasMaxLength(320).IsRequired();
        emailConfig.Property(item => item.LastUidValidity).HasMaxLength(64);
        emailConfig.Property(item => item.SmtpServer).HasMaxLength(255);
        emailConfig.Property(item => item.EncryptedPassword).HasMaxLength(2048).IsRequired();
        emailConfig.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<CoordinatorEmailConfig>(item => item.CoordinatorId)
            .OnDelete(DeleteBehavior.Cascade);
        emailConfig.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        var mailRule = modelBuilder.Entity<ClientMailRule>();
        mailRule.HasKey(item => item.Id);
        mailRule.HasIndex(item => new { item.ManagerId, item.Pattern }).IsUnique();
        mailRule.HasQueryFilter(item => IsTenantAdmin || TenantId != null && item.ManagerId == TenantId);
        mailRule.Property(item => item.Pattern).HasMaxLength(320).IsRequired();
        mailRule.Ignore(item => item.IsDomain);
        mailRule.HasOne<AppUser>().WithMany().HasForeignKey(item => item.ManagerId).OnDelete(DeleteBehavior.Restrict);
        mailRule.HasOne<AppUser>().WithMany().HasForeignKey(item => item.CreatedById).OnDelete(DeleteBehavior.Restrict);
        mailRule.HasOne<Client>().WithMany().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.SetNull);

        var outboundEmail = modelBuilder.Entity<OutboundEmail>();
        outboundEmail.HasKey(item => item.Id);
        outboundEmail.HasIndex(item => new { item.QuoteId, item.Kind }).IsUnique();
        outboundEmail.HasIndex(item => item.RequestId);
        outboundEmail.HasQueryFilter(item => IsTenantAdmin || TenantId != null && item.ManagerId == TenantId);
        outboundEmail.Property(item => item.Kind).HasConversion<string>().HasMaxLength(16);
        outboundEmail.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
        outboundEmail.Property(item => item.ToAddress).HasMaxLength(320).IsRequired();
        outboundEmail.Property(item => item.ToName).HasMaxLength(500);
        outboundEmail.Property(item => item.FromAddress).HasMaxLength(320).IsRequired();
        outboundEmail.Property(item => item.FromName).HasMaxLength(500);
        outboundEmail.Property(item => item.Subject).HasMaxLength(998).IsRequired();
        outboundEmail.Property(item => item.MessageId).HasMaxLength(998).IsRequired();
        outboundEmail.Property(item => item.InReplyTo).HasMaxLength(998);
        outboundEmail.Property(item => item.AttachmentFileName).HasMaxLength(260);
        outboundEmail.Property(item => item.LastError).HasMaxLength(500);
        outboundEmail.Ignore(item => item.IsStalePending);
        outboundEmail.HasOne<Quote>().WithMany().HasForeignKey(item => item.QuoteId).OnDelete(DeleteBehavior.Cascade);
        outboundEmail.HasOne<InspectionRequest>().WithMany().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);

        var quotationDraft = modelBuilder.Entity<QuotationDraft>();
        quotationDraft.HasKey(item => item.Id);
        quotationDraft.HasIndex(item => item.TenantId);
        quotationDraft.HasIndex(item => new
        {
            item.CoordinatorId,
            item.SourceUidValidity,
            item.SourceUid
        }).IsUnique();
        quotationDraft.HasQueryFilter(item =>
            IsTenantAdmin || TenantId != null && item.TenantId == TenantId);
        quotationDraft.Property(item => item.ClientName).HasMaxLength(200);
        quotationDraft.Property(item => item.Location).HasMaxLength(500);
        quotationDraft.Property(item => item.MachineDetails).HasMaxLength(4000);
        quotationDraft.Property(item => item.AmountPiastres);
        quotationDraft.Property(item => item.ValidUntil);
        quotationDraft.Property(item => item.RawEmailBody).IsRequired();
        quotationDraft.Property(item => item.EmailHtmlBody);
        quotationDraft.Property(item => item.SenderEmail).HasMaxLength(320);
        quotationDraft.Property(item => item.SenderName).HasMaxLength(500);
        quotationDraft.Property(item => item.EmailSubject).HasMaxLength(998);
        quotationDraft.Property(item => item.ToAddressesJson);
        quotationDraft.Property(item => item.CcAddressesJson);
        quotationDraft.Property(item => item.BccAddressesJson);
        quotationDraft.Property(item => item.ReplyToAddressesJson);
        quotationDraft.Property(item => item.AttachmentMetadataJson);
        quotationDraft.Property(item => item.EmailMessageId).HasMaxLength(998);
        quotationDraft.Property(item => item.InReplyTo).HasMaxLength(998);
        quotationDraft.Property(item => item.References);
        quotationDraft.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        quotationDraft.Property(item => item.SourceUidValidity).HasMaxLength(64).IsRequired();
        quotationDraft.HasOne(item => item.Coordinator)
            .WithMany()
            .HasForeignKey(item => item.CoordinatorId)
            .OnDelete(DeleteBehavior.Restrict);
        quotationDraft.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Every Guid key is created by the application. Left as "generated on add", EF treats a new entity that was added
        // through a tracked parent (a status history row, for example) as already existing and updates it instead of inserting.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindPrimaryKey()?.Properties is [{ ClrType: var keyType } key] && keyType == typeof(Guid))
                key.ValueGenerated = ValueGenerated.Never;
        }
    }
}