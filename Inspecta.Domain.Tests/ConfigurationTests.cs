using System.Security.Claims;
using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Contracts.Equipment;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>Clients with their contacts and the items registered for them, saved through the real repositories.</summary>
public sealed class ConfigurationTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);

    public ConfigurationTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Users.Add(manager);
        db.SaveChanges();
    }

    [Fact]
    public async Task AClientIsCreatedWithItsContactsAndEmails()
    {
        await using var db = CreateContext();

        var created = await new ClientService(new EfClientRepository(db), new EfUserRepository(db)).CreateAsync(
            NewClient(), manager.Id);

        await using var check = CreateContext();
        var saved = await new ClientService(new EfClientRepository(check), new EfUserRepository(check)).GetByIdAsync(created.Id);
        Assert.Equal("Al Noor Industries", saved!.Name);
        Assert.Equal("certificates@alnoor.com", saved.CertificatesEmail);
        Assert.Equal(["Mona", "Samir"], saved.Contacts.Select(contact => contact.Name).Order());
        Assert.Equal("mona@alnoor.com", saved.Contacts.Single(contact => contact.IsPrimary).Email);
    }

    [Fact]
    public async Task ContactsCanBeEditedAddedAndRemovedWhenUpdatingAClient()
    {
        var clientId = await CreateClientAsync();
        await using var db = CreateContext();
        var service = new ClientService(new EfClientRepository(db), new EfUserRepository(db));
        var before = (await service.GetByIdAsync(clientId))!;
        var mona = before.Contacts.Single(contact => contact.Name == "Mona");

        await service.UpdateAsync(clientId, new UpdateClientRequest
        {
            Name = "Al Noor Industries",
            Contacts =
            [
                new CreateClientContactRequest { Id = mona.Id, Name = "Mona", Email = "mona.new@alnoor.com", IsPrimary = true },
                new CreateClientContactRequest { Name = "Hany", Email = "hany@alnoor.com" }
            ]
        });

        await using var check = CreateContext();
        var after = (await new ClientService(new EfClientRepository(check), new EfUserRepository(check)).GetByIdAsync(clientId))!;
        Assert.Equal(["Hany", "Mona"], after.Contacts.Select(contact => contact.Name).Order());
        Assert.Equal(mona.Id, after.Contacts.Single(contact => contact.Name == "Mona").Id);
        Assert.Equal("mona.new@alnoor.com", after.Contacts.Single(contact => contact.Name == "Mona").Email);
    }

    [Fact]
    public async Task LeavingContactsOutKeepsThem()
    {
        var clientId = await CreateClientAsync();
        await using var db = CreateContext();
        var service = new ClientService(new EfClientRepository(db), new EfUserRepository(db));

        var updated = await service.UpdateAsync(clientId, new UpdateClientRequest { Name = "Al Noor Industries Ltd." });

        Assert.Equal("Al Noor Industries Ltd.", updated.Name);
        Assert.Equal(2, updated.Contacts.Count);
    }

    [Fact]
    public async Task AnItemIsRegisteredForAClientWithItsStandardOfInspection()
    {
        var clientId = await CreateClientAsync();
        await using var db = CreateContext();
        var service = new EquipmentService(new EfEquipmentRepository(db));

        var created = await service.CreateAsync(new SaveEquipmentRequest
        {
            ClientId = clientId,
            Name = "Mobile crane",
            Capacity = "100 ton",
            SerialNumber = "LTM-1100",
            InspectionStandard = "  ASME B30.5 "
        });

        await using var check = CreateContext();
        var saved = (await new EquipmentService(new EfEquipmentRepository(check)).GetByIdAsync(created.Id))!;
        Assert.Equal(clientId, saved.ClientId);
        Assert.Equal("ASME B30.5", saved.InspectionStandard);
        Assert.Single(await new EquipmentService(new EfEquipmentRepository(check)).ListAsync(clientId, includeArchived: false));
    }

    [Fact]
    public async Task TheStandardCanBeChangedAndCleared()
    {
        var clientId = await CreateClientAsync();
        await using var db = CreateContext();
        var service = new EquipmentService(new EfEquipmentRepository(db));
        var created = await service.CreateAsync(new SaveEquipmentRequest
        {
            ClientId = clientId, Name = "Air receiver", InspectionStandard = "PED 2014/68/EU"
        });

        var changed = await service.UpdateAsync(created.Id, new SaveEquipmentRequest
        {
            ClientId = clientId, Name = "Air receiver", InspectionStandard = "EN 13445"
        });
        Assert.Equal("EN 13445", changed.InspectionStandard);

        var kept = await service.UpdateAsync(created.Id, new SaveEquipmentRequest { ClientId = clientId, Name = "Air receiver" });
        Assert.Equal("EN 13445", kept.InspectionStandard);

        var cleared = await service.UpdateAsync(created.Id, new SaveEquipmentRequest
        {
            ClientId = clientId, Name = "Air receiver", InspectionStandard = ""
        });
        Assert.Null(cleared.InspectionStandard);
    }

    [Fact]
    public async Task AnItemNeedsAnExistingClientAndCannotMoveToAnother()
    {
        var clientId = await CreateClientAsync();
        await using var db = CreateContext();
        var service = new EquipmentService(new EfEquipmentRepository(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(
            new SaveEquipmentRequest { ClientId = Guid.NewGuid(), Name = "Crane" }));

        var created = await service.CreateAsync(new SaveEquipmentRequest { ClientId = clientId, Name = "Crane" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            created.Id, new SaveEquipmentRequest { ClientId = Guid.NewGuid(), Name = "Crane" }));
    }

    [Fact]
    public async Task AClientNothingUsesCanBeDeletedForGoodWithItsContacts()
    {
        var clientId = await CreateClientAsync();
        await using var db = CreateContext();

        await new ClientService(new EfClientRepository(db), new EfUserRepository(db)).DeleteAsync(clientId);

        await using var check = CreateContext();
        Assert.Null(await new ClientService(new EfClientRepository(check), new EfUserRepository(check)).GetByIdAsync(clientId));
        Assert.Empty(check.ClientContacts.IgnoreQueryFilters().ToList());
    }

    [Fact]
    public async Task AClientWithItemsCannotBeDeletedForGoodButCanBeArchived()
    {
        var clientId = await CreateClientAsync();
        await using (var db = CreateContext())
            await new EquipmentService(new EfEquipmentRepository(db)).CreateAsync(new SaveEquipmentRequest { ClientId = clientId, Name = "Crane" });

        await using var attempt = CreateContext();
        var service = new ClientService(new EfClientRepository(attempt), new EfUserRepository(attempt));
        var refused = await Assert.ThrowsAsync<Inspecta.Application.Exceptions.AuthException>(() => service.DeleteAsync(clientId));
        Assert.Equal("client_in_use", refused.Code);
        Assert.Equal(409, refused.StatusCode);
        Assert.Contains("1 item(s)", refused.Message);
        Assert.NotNull(await service.GetByIdAsync(clientId));

        await service.ArchiveAsync(clientId);
        await using var check = CreateContext();
        Assert.NotNull((await new ClientService(new EfClientRepository(check), new EfUserRepository(check)).GetByIdAsync(clientId))!.ArchivedAt);
    }

    [Fact]
    public async Task DeletingAnUnknownClientIsNotFound()
    {
        await using var db = CreateContext();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ClientService(new EfClientRepository(db), new EfUserRepository(db)).DeleteAsync(Guid.NewGuid()));
    }

    private async Task<Guid> CreateClientAsync()
    {
        await using var db = CreateContext();
        return (await new ClientService(new EfClientRepository(db), new EfUserRepository(db)).CreateAsync(NewClient(), manager.Id)).Id;
    }

    private static CreateClientRequest NewClient() => new()
    {
        Name = "Al Noor Industries",
        IsCompany = true,
        HasContract = true,
        Location = "Cairo",
        CertificatesEmail = "certificates@alnoor.com",
        Contacts =
        [
            new CreateClientContactRequest { Name = "Mona", Email = "mona@alnoor.com", Phone = "0100", IsPrimary = true },
            new CreateClientContactRequest { Name = "Samir", Email = "samir@alnoor.com" }
        ]
    };

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
