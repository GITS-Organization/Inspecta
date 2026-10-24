using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Data;

public sealed class InspectaDbContext(DbContextOptions<InspectaDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<InspectionRequest> Requests => Set<InspectionRequest>();
    public DbSet<RequestItem> RequestItems => Set<RequestItem>();
    public DbSet<JobStatusHistory> JobStatusHistory => Set<JobStatusHistory>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<InspectionCertificate> Certificates => Set<InspectionCertificate>();
    public DbSet<TimesheetEntry> Timesheets => Set<TimesheetEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.HasKey(item => item.Id);
        user.Property(item => item.FirebaseUid).HasMaxLength(128);
        user.HasIndex(item => item.FirebaseUid).IsUnique();
        user.Property(item => item.Email).HasMaxLength(320).IsRequired();
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.FullName).HasMaxLength(200).IsRequired();
        user.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(item => item.Role).HasConversion<string>().HasMaxLength(32);
        user.Property(item => item.QualificationsJson).HasMaxLength(4000).IsRequired();

        var refreshToken = modelBuilder.Entity<RefreshToken>();
        refreshToken.HasKey(item => item.Id);
        refreshToken.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
        refreshToken.HasIndex(item => item.TokenHash).IsUnique();
        refreshToken.HasIndex(item => new { item.UserId, item.FamilyId });
        refreshToken.HasOne(item => item.User)
            .WithMany(item => item.RefreshTokens)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var client = modelBuilder.Entity<Client>();
        client.HasKey(item => item.Id);
        client.Property(item => item.Name).HasMaxLength(200).IsRequired();
        client.Property(item => item.LegacyId).HasMaxLength(128);
        client.HasIndex(item => item.LegacyId).IsUnique();
        client.HasMany(item => item.Contacts).WithOne().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Cascade);

        var contact = modelBuilder.Entity<ClientContact>();
        contact.HasKey(item => item.Id);
        contact.Property(item => item.Name).HasMaxLength(200).IsRequired();
        contact.Property(item => item.Email).HasMaxLength(320);

        var equipment = modelBuilder.Entity<Equipment>();
        equipment.HasKey(item => item.Id);
        equipment.Property(item => item.Name).HasMaxLength(200).IsRequired();
        equipment.Property(item => item.LegacyId).HasMaxLength(128);
        equipment.HasIndex(item => item.LegacyId).IsUnique();
        equipment.HasOne<Client>().WithMany().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Restrict);

        var request = modelBuilder.Entity<InspectionRequest>();
        request.HasKey(item => item.Id);
        request.Property(item => item.LegacyId).HasMaxLength(128);
        request.HasIndex(item => item.LegacyId).IsUnique();
        request.Property(item => item.ClientName).HasMaxLength(200).IsRequired();
        request.Property(item => item.Location).HasMaxLength(500).IsRequired();
        request.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        request.HasMany(item => item.Items).WithOne().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
        request.HasMany(item => item.History).WithOne().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
        request.HasOne<Client>().WithMany().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Restrict);

        var requestItem = modelBuilder.Entity<RequestItem>();
        requestItem.HasKey(item => item.Id);
        requestItem.Property(item => item.Name).HasMaxLength(200).IsRequired();
        requestItem.HasOne<Equipment>().WithMany().HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.SetNull);

        var history = modelBuilder.Entity<JobStatusHistory>();
        history.HasKey(item => item.Id);
        history.Property(item => item.FromStatus).HasConversion<string>();
        history.Property(item => item.ToStatus).HasConversion<string>();
        history.HasIndex(item => new { item.RequestId, item.OccurredAt });

        var quote = modelBuilder.Entity<Quote>();
        quote.HasKey(item => item.Id);
        quote.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        quote.HasIndex(item => new { item.RequestId, item.Version }).IsUnique();
        quote.HasOne<InspectionRequest>().WithMany().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);

        var certificate = modelBuilder.Entity<InspectionCertificate>();
        certificate.HasKey(item => item.Id);
        certificate.HasIndex(item => item.RequestId).IsUnique();
        certificate.Property(item => item.FunctionCheck).HasConversion<string>().HasMaxLength(24);
        certificate.Property(item => item.FinalResult).HasConversion<string>().HasMaxLength(32);
        certificate.HasOne<InspectionRequest>().WithOne().HasForeignKey<InspectionCertificate>(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);

        var timesheet = modelBuilder.Entity<TimesheetEntry>();
        timesheet.HasKey(item => item.Id);
        timesheet.HasIndex(item => item.RequestId).IsUnique();
        timesheet.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
        timesheet.HasOne<InspectionRequest>().WithOne().HasForeignKey<TimesheetEntry>(item => item.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}