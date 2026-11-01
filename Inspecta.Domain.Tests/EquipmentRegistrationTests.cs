using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

public sealed class EquipmentRegistrationTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid CoordinatorId = Guid.NewGuid();

    [Fact]
    public async Task ANewMachineFromTheScreenIsRegisteredAndTheRequestStillSaves()
    {
        var fixture = new Fixture();
        var screenId = Guid.NewGuid();

        // The shape the mobile app sent: no client yet, a typed client name, and one machine with an id made on the phone.
        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Detla Steal",
            Location = "cairo- egypt",
            AccessNotes = "some notes here",
            Items =
            [
                new SaveRequestItem { Id = screenId, Name = "Equiment", Type = "Equiment", Quantity = 1, Capacity = "2 tons", Location = "cairo- egypt" }
            ]
        });

        var client = Assert.Single(fixture.Catalog.StagedClients);
        Assert.Equal("Detla Steal", client.Name);
        Assert.Equal(ManagerId, client.ManagerId);
        Assert.Equal(client.Id, saved.ClientId);
        var equipment = Assert.Single(fixture.Catalog.StagedEquipment);
        Assert.Equal(client.Id, equipment.ClientId);
        Assert.Equal("Equiment", equipment.Name);
        Assert.Equal("2 tons", equipment.Capacity);
        Assert.Equal("cairo- egypt", equipment.Location);
        var item = Assert.Single(saved.Items);
        Assert.Equal(screenId, item.Id);
        Assert.Equal(equipment.Id, item.EquipmentId);
        Assert.Equal(1, fixture.Repository.Saves);
        Assert.Empty(saved.MissingFields);
    }

    [Fact]
    public async Task AMachineAlreadyInTheCatalogIsLinkedInsteadOfDuplicated()
    {
        var fixture = new Fixture();
        var client = Client.Create("Al Noor", managerId: ManagerId);
        var existing = Item.Create(client.Id, "Air receiver");
        existing.UpdateDetails("Air receiver", null, null, null, "SN-1", "3000 L");
        fixture.Catalog.Clients.Add(client);
        fixture.Catalog.Equipment.Add(existing);

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientId = client.Id,
            ClientName = "Al Noor",
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = " air RECEIVER ", SerialNumber = "sn-1", Capacity = "3000 L" }]
        });

        Assert.Empty(fixture.Catalog.StagedEquipment);
        Assert.Empty(fixture.Catalog.StagedClients);
        Assert.Equal(existing.Id, Assert.Single(saved.Items).EquipmentId);
    }

    [Fact]
    public async Task ADifferentSerialNumberIsADifferentMachine()
    {
        var fixture = new Fixture();
        var client = Client.Create("Al Noor", managerId: ManagerId);
        var existing = Item.Create(client.Id, "Air receiver");
        existing.UpdateDetails("Air receiver", null, null, null, "SN-1", "3000 L");
        fixture.Catalog.Clients.Add(client);
        fixture.Catalog.Equipment.Add(existing);

        await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientId = client.Id,
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Air receiver", SerialNumber = "SN-2", Capacity = "3000 L" }]
        });

        Assert.Equal("SN-2", Assert.Single(fixture.Catalog.StagedEquipment).SerialNumber);
    }

    [Fact]
    public async Task AnExistingClientOfTheTeamIsFoundByNameInsteadOfCreatingADuplicate()
    {
        var fixture = new Fixture();
        var client = Client.Create("Al Noor Heavy Industries", managerId: ManagerId);
        fixture.Catalog.Clients.Add(client);

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "  al noor heavy industries ",
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane" }]
        });

        Assert.Empty(fixture.Catalog.StagedClients);
        Assert.Equal(client.Id, saved.ClientId);
        Assert.Equal(client.Id, Assert.Single(fixture.Catalog.StagedEquipment).ClientId);
    }

    [Fact]
    public async Task ClientsOfOtherTeamsAreNotReused()
    {
        var fixture = new Fixture();
        fixture.Catalog.Clients.Add(Client.Create("Al Noor", managerId: Guid.NewGuid()));

        await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Al Noor",
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane" }]
        });

        Assert.Equal(ManagerId, Assert.Single(fixture.Catalog.StagedClients).ManagerId);
    }

    [Fact]
    public async Task WithoutAClientNameNothingIsRegisteredButTheRequestStillSaves()
    {
        var fixture = new Fixture();

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane" }]
        });

        Assert.Empty(fixture.Catalog.StagedClients);
        Assert.Empty(fixture.Catalog.StagedEquipment);
        Assert.Null(Assert.Single(saved.Items).EquipmentId);
        Assert.Equal(1, fixture.Repository.Saves);
    }

    [Fact]
    public async Task ItemsAlreadyLinkedToAMachineAreLeftAlone()
    {
        var fixture = new Fixture();
        var client = Client.Create("Al Noor", managerId: ManagerId);
        fixture.Catalog.Clients.Add(client);
        var machine = Item.Create(client.Id, "Crane");
        fixture.Catalog.Equipment.Add(machine);

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientId = client.Id,
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane", EquipmentId = machine.Id }]
        });

        Assert.Empty(fixture.Catalog.StagedEquipment);
        Assert.Equal(machine.Id, Assert.Single(saved.Items).EquipmentId);
    }

    [Fact]
    public async Task AMachineOfAnotherClientOrAnUnknownOneIsRefused()
    {
        var fixture = new Fixture();
        var client = Client.Create("Al Noor", managerId: ManagerId);
        fixture.Catalog.Clients.Add(client);
        var foreign = Item.Create(Guid.NewGuid(), "Crane");
        fixture.Catalog.Equipment.Add(foreign);

        var other = await Assert.ThrowsAsync<ArgumentException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientId = client.Id,
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane", EquipmentId = foreign.Id }]
        }));
        Assert.Contains("does not belong to the client", other.Message, StringComparison.Ordinal);

        var unknown = await Assert.ThrowsAsync<ArgumentException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientId = client.Id,
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane", EquipmentId = Guid.NewGuid() }]
        }));
        Assert.Contains("not found", unknown.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Repository.Saves);
    }

    [Fact]
    public async Task WithoutAutoRegistrationTypedMachinesAndClientsAreOnlyLinkedNeverCreated()
    {
        var fixture = new Fixture(createMissing: false);
        var client = Client.Create("Al Noor", managerId: ManagerId);
        fixture.Catalog.Clients.Add(client);
        var known = Item.Create(client.Id, "Crane");
        fixture.Catalog.Equipment.Add(known);

        var typedClient = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Brand New Client",
            Location = "Cairo",
            Items = [new SaveRequestItem { Name = "Crane" }]
        });
        Assert.Empty(fixture.Catalog.StagedClients);
        Assert.Null(typedClient.ClientId);

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientId = client.Id,
            ClientName = "Al Noor",
            Location = "Cairo",
            Items =
            [
                new SaveRequestItem { Name = "Crane" },
                new SaveRequestItem { Name = "Mystery machine" }
            ]
        });

        Assert.Empty(fixture.Catalog.StagedEquipment);
        Assert.Equal(known.Id, saved.Items[0].EquipmentId);
        Assert.Null(saved.Items[1].EquipmentId);
    }

    [Fact]
    public async Task TheSameMachineTwiceInOneSaveSharesOneRecord()
    {
        var fixture = new Fixture();

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Al Noor",
            Location = "Cairo",
            Items =
            [
                new SaveRequestItem { Name = "Safety valve", Capacity = "DN40" },
                new SaveRequestItem { Name = "safety valve", Capacity = "dn40" },
                new SaveRequestItem { Name = "Safety valve", Capacity = "DN50" }
            ]
        });

        Assert.Equal(2, fixture.Catalog.StagedEquipment.Count);
        Assert.Equal(saved.Items[0].EquipmentId, saved.Items[1].EquipmentId);
        Assert.NotEqual(saved.Items[0].EquipmentId, saved.Items[2].EquipmentId);
    }

    [Fact]
    public async Task AMachineAddedOnItsOwnIsRegisteredToo()
    {
        var fixture = new Fixture();
        await fixture.SaveAsync(new UpdateRequestDetailsRequest { ClientName = "Al Noor", Location = "Cairo" });

        var saved = await fixture.Service.AddItemAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new CreateRequestItem { Name = "Boiler", Quantity = 2 });

        Assert.Equal("Boiler", Assert.Single(fixture.Catalog.StagedEquipment).Name);
        Assert.NotNull(Assert.Single(saved.Items).EquipmentId);
    }

    private sealed class Fixture
    {
        public Fixture(bool createMissing = true)
        {
            Request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, new IncomingEmail(
                "42", 1, "Please quote.", null, "buyer@alnoor.com", "Al Noor", "Quote request",
                [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)));
            Repository = new RequestSaveTests.Repo(Request) { ClientManagerId = ManagerId };
            Catalog = new FakeCatalog();
            Service = new InspectionRequestService(Repository, null!, new RequestEquipmentRegistrar(Catalog, createMissing));
        }

        public InspectionRequest Request { get; }
        public RequestSaveTests.Repo Repository { get; }
        public FakeCatalog Catalog { get; }
        public InspectionRequestService Service { get; }

        public Task<RequestResponse> SaveAsync(UpdateRequestDetailsRequest details) =>
            Service.UpdateDetailsAsync(Request.Id, CoordinatorId, "coordinator", details);
    }

    private sealed class FakeCatalog : IEquipmentCatalog
    {
        public List<Client> Clients { get; } = [];
        public List<Item> Equipment { get; } = [];
        public List<Client> StagedClients { get; } = [];
        public List<Item> StagedEquipment { get; } = [];

        public Task<Client?> FindClientByNameAsync(Guid managerId, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clients.FirstOrDefault(client =>
                client.ManagerId == managerId && string.Equals(client.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<Item?> FindEquipmentAsync(
            Guid clientId, string name, string? serialNumber, string? capacity, CancellationToken cancellationToken = default) =>
            Task.FromResult(Equipment.FirstOrDefault(item =>
                item.ClientId == clientId &&
                string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Capacity, capacity, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyDictionary<Guid, Guid>> GetEquipmentOwnersAsync(
            IReadOnlyCollection<Guid> equipmentIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(
                Equipment.Where(item => equipmentIds.Contains(item.Id)).ToDictionary(item => item.Id, item => item.ClientId));

        public Task<IReadOnlyList<Item>> ListClientEquipmentAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Item>>(Equipment.Where(item => item.ClientId == clientId).ToArray());

        public void Stage(Client client) => StagedClients.Add(client);

        public void Stage(Item equipment) => StagedEquipment.Add(equipment);
    }
}
