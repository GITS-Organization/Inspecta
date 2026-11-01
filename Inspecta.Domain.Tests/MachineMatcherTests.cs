using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

/// <summary>Reading an incoming email and finding which machines of the client it asks about.</summary>
public sealed class MachineMatcherTests
{
    private static readonly MachineCandidate AirReceiver = Machine("Air Receiver", capacity: "10 bar", serial: "AR-2024-0173");
    private static readonly MachineCandidate Crane = Machine("Mobile Crane", capacity: "50 ton", model: "LTM 1050", manufacturer: "Liebherr");
    private static readonly MachineCandidate Vessel = Machine("Pressure Vessel", serial: "PV55012");
    private static readonly MachineCandidate Pump = Machine("Pump");
    private static readonly MachineCandidate ArabicCrane = Machine("رافعة متحركة");
    private static readonly MachineCandidate GenericOne = Machine("Equipment");
    private static readonly MachineCandidate Sling2 = Machine("Lifting Sling", capacity: "2 ton");
    private static readonly MachineCandidate Sling5 = Machine("Lifting Sling", capacity: "5 ton");

    private static MachineCandidate Machine(string name, string? capacity = null, string? serial = null, string? model = null, string? manufacturer = null) =>
        new(Guid.NewGuid(), name, null, manufacturer, model, serial, null, capacity, null);

    private static IReadOnlyList<MachineMatch> Read(string body, string? subject = null, params MachineCandidate[] machines) =>
        MachineMatcher.Match(EmailTextCleaner.NewText(subject, body, null), machines.Length == 0 ? [AirReceiver, Crane, Vessel, Pump, ArabicCrane, GenericOne, Sling2, Sling5] : machines);

    private static MachineMatch Of(IReadOnlyList<MachineMatch> matches, MachineCandidate machine) =>
        Assert.Single(matches, match => match.Machine.EquipmentId == machine.EquipmentId);

    [Fact]
    public void ANumberedListGivesTheMachinesAndTheirQuantities()
    {
        var matches = Read("Dear Hager,\n\nPlease quote for the following:\n1. 4 air receivers\n2. Mobile crane x2\n\nRegards,\nBuyer");

        var receiver = Of(matches, AirReceiver);
        Assert.Equal(4, receiver.Quantity);
        Assert.True(receiver.IsConfident);
        Assert.Contains("air receivers", receiver.Evidence, StringComparison.Ordinal);
        Assert.Equal(2, Of(matches, Crane).Quantity);
        Assert.Equal(2, matches.Count);
    }

    [Theory]
    [InlineData("We need the air receiver (4 nos) inspected.", 4)]
    [InlineData("Air receiver x4 for the plant", 4)]
    [InlineData("air receiver - qty: 3", 3)]
    [InlineData("air receiver 6 pcs", 6)]
    [InlineData("2. air receiver", 1)]
    [InlineData("item 3 air receiver", 1)]
    [InlineData("two inspections of the air receiver please", 1)]
    public void TheQuantityIsReadFromTheWordsAroundTheName(string line, int expected) =>
        Assert.Equal(expected, Assert.Single(Read(line, null, AirReceiver)).Quantity);

    [Fact]
    public void ASerialNumberIsEnoughEvenWrittenAnotherWayAndTheQuantityIsOne()
    {
        var matches = Read("Please inspect the unit with serial AR20240173 urgently.", null, AirReceiver);

        var match = Assert.Single(matches);
        Assert.Equal(MachineMatchReason.Serial, match.Reason);
        Assert.Equal(1, match.Confidence);
        Assert.Equal(1, match.Quantity);

        Assert.Equal(MachineMatchReason.Serial, Assert.Single(Read("S/N: pv 55012", null, Vessel)).Reason);
    }

    [Fact]
    public void AShortSerialIsFoundWhetherItIsDashedSpacedOrRunTogether()
    {
        var hoist = Machine("Electric Wire Rope Hoist", serial: "WH-002");

        foreach (var text in new[] { "Item ID: WH-002", "Item ID: WH 002", "item id wh002", "ID WH – 002 please" })
        {
            var match = Assert.Single(MachineMatcher.Match(EmailTextCleaner.NewText(null, text, null), [hoist]));
            Assert.Equal(MachineMatchReason.Serial, match.Reason);
            Assert.True(match.IsConfident);
        }

        Assert.Empty(MachineMatcher.Match("Item ID: WH-003", [hoist]));
    }

    [Fact]
    public void ThePluralsAndASmallTypoStillFindTheMachineButAreOnlyASuggestion()
    {
        Assert.True(Assert.Single(Read("Kindly inspect our pressure vessels", null, Vessel)).IsConfident);

        var typo = Assert.Single(Read("Need a mobil crane next week", null, Crane));
        Assert.False(typo.IsConfident);
        Assert.InRange(typo.Confidence, 0.6, 0.85);
    }

    [Fact]
    public void AnArabicEmailIsMatchedAfterLetterFormsAndDigitsAreMadeUniform()
    {
        var match = Assert.Single(Read("السلام عليكم، نحتاج فحص الرافعة المتحركة عدد ٢", null, ArabicCrane));

        Assert.Equal(2, match.Quantity);
        Assert.True(match.IsConfident);
    }

    [Fact]
    public void AMachineMentionedOnlyInTheQuotedOlderMessageIsIgnored()
    {
        var body = "Thanks, please go ahead with the crane.\n\nOn Mon, 27 Oct 2026 at 10:00, Hager <h@integrity-intl.com> wrote:\n> The pressure vessel and the mobile crane are covered.\n> pump too";

        var matches = Read(body, null, Vessel, Pump, Crane);

        Assert.Empty(matches.Where(match => match.Machine == Vessel || match.Machine == Pump));
    }

    [Fact]
    public void AForwardedRequestIsReadBelowTheForwardMarker()
    {
        var body = "FYI\n\n---------- Forwarded message ---------\nFrom: Boss <boss@alnoor.com>\nSent: Monday, 27 October 2026 10:00\nTo: Buyer <buyer@alnoor.com>\nSubject: Inspection\n\nPlease inspect 2 pressure vessel PV55012.";

        var match = Assert.Single(Read(body, null, Vessel));

        Assert.Equal(MachineMatchReason.Serial, match.Reason);
    }

    [Fact]
    public void AnHtmlOnlyEmailIsReadWithoutItsQuotedBlockOrStyles()
    {
        var html = "<html><style>.x{color:red}</style><body><p>Please inspect the <b>air receiver</b> x3</p><blockquote>pump and mobile crane</blockquote></body></html>";

        var matches = MachineMatcher.Match(EmailTextCleaner.NewText("Inspection", null, html), [AirReceiver, Pump, Crane]);

        Assert.Equal(3, Assert.Single(matches).Quantity);
    }

    [Fact]
    public void SeveralSimilarMachinesAreAmbiguousUnlessTheEmailTellsThemApart()
    {
        var vague = Read("We need 3 lifting slings inspected", null, Sling2, Sling5);

        Assert.Equal(2, vague.Count);
        Assert.All(vague, match =>
        {
            Assert.True(match.Ambiguous);
            Assert.False(match.IsConfident);
        });

        var clear = Read("We need the 5 ton lifting sling inspected", null, Sling2, Sling5);

        var only = Assert.Single(clear);
        Assert.Equal(Sling5.EquipmentId, only.Machine.EquipmentId);
        Assert.True(only.IsConfident);
    }

    [Fact]
    public void ASingleCommonWordIsNeverEnoughToAddAMachineByItself()
    {
        var pump = Assert.Single(Read("The pump house will be closed on Friday", null, Pump));

        Assert.False(pump.IsConfident);
        Assert.Empty(Read("Please inspect the equipment", null, GenericOne));
    }

    [Fact]
    public void ModelAndCapacityMakeANameMatchStronger()
    {
        var plain = Of(Read("inspect mobile crane", null, Crane), Crane).Confidence;
        var detailed = Of(Read("inspect mobile crane Liebherr LTM 1050 50 ton", null, Crane), Crane).Confidence;

        Assert.True(detailed > plain);
        Assert.Equal(MachineMatchReason.Model, Assert.Single(Read("Please check LTM-1050", null, Crane)).Reason);
    }

    [Fact]
    public void NothingFoundAndHugeOrEmptyInputAreHandled()
    {
        Assert.Empty(Read("Hello, are you open on Friday?"));
        Assert.Empty(MachineMatcher.Match(null, [AirReceiver]));
        Assert.Empty(MachineMatcher.Match("air receiver", []));
        Assert.True(EmailTextCleaner.NewText(null, new string('x', 500_000), null).Length <= EmailTextCleaner.MaxLength);
    }

    [Fact]
    public async Task OnlyConfidentMachinesGoOnARequestWithAClientAndNoItems()
    {
        var clientId = Guid.NewGuid();
        var catalog = new FakeCatalog(clientId, ToItem(clientId, AirReceiver), ToItem(clientId, Pump), ToItem(clientId, Crane));
        var service = new MachineSuggestionService(catalog);
        var request = NewRequest(clientId);

        var added = await service.AutofillAsync(request, "Please quote 4 air receivers and the pump.", null);

        var item = Assert.Single(added);
        Assert.Equal(4, item.Quantity);
        Assert.Equal("Air Receiver", item.Name);
        Assert.Equal(catalog.Machines.First(machine => machine.Name == "Air Receiver").Id, item.EquipmentId);
        Assert.Equal("10 bar", item.Capacity);
        Assert.Same(item, Assert.Single(request.Items));

        // The pump was only a suggestion; and once the request has items nothing more is added.
        var suggestions = await service.FindAsync(request, "Please quote 4 air receivers and the pump.", null);
        Assert.Equal("Pump", Assert.Single(suggestions).Machine.Name);
        Assert.Empty(await service.AutofillAsync(request, "mobile crane", null));
    }

    [Fact]
    public async Task ARequestWithoutAClientGetsNothing()
    {
        var clientId = Guid.NewGuid();
        var service = new MachineSuggestionService(new FakeCatalog(clientId, ToItem(clientId, AirReceiver)));
        var request = NewRequest(null);

        Assert.Empty(await service.FindAsync(request, "4 air receivers", null));
        Assert.Empty(await service.AutofillAsync(request, "4 air receivers", null));
        Assert.Empty(request.Items);
    }

    [Fact]
    public async Task TheCoordinatorCanReadTheEmailAgainAndSeeTheSuggestions()
    {
        var managerId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var request = InspectionRequest.CreateFromEmail(
            managerId, coordinatorId,
            new IncomingEmail("42", 1, null, null, "buyer@alnoor.com", "Buyer", "Quote", [], [], [], [], [], "<id@alnoor.com>", null, null, null),
            clientId: clientId, clientName: "Al Noor");
        var repository = new RequestSaveTests.Repo(request);
        var service = new InspectionRequestService(repository, null!, suggestions: new StubSuggestions(AirReceiver));

        var suggested = Assert.Single(await service.SuggestMachinesAsync(request.Id, coordinatorId, "coordinator"));
        Assert.Equal("high", suggested.Level);
        Assert.Equal("name", suggested.Reason);
        Assert.Equal(4, suggested.Quantity);

        var saved = await service.AutofillAsync(request.Id, coordinatorId, "coordinator");

        var item = Assert.Single(saved.Items);
        Assert.Equal(AirReceiver.EquipmentId, item.EquipmentId);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(1, repository.Saves);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SuggestMachinesAsync(request.Id, Guid.NewGuid(), "coordinator"));
    }

    private sealed class StubSuggestions(MachineCandidate machine) : IMachineSuggestionService
    {
        public Task<IReadOnlyList<MachineMatch>> FindAsync(InspectionRequest request, string? textBody, string? htmlBody, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MachineMatch>>([new MachineMatch(machine, 4, 0.92, MachineMatchReason.Name, "4 air receivers", false)]);

        public Task<IReadOnlyList<RequestItem>> AutofillAsync(InspectionRequest request, string? textBody, string? htmlBody, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestItem>>([request.AddItem(RequestItem.Create(machine.Name, 4, machine.EquipmentId))]);
    }

    private static InspectionRequest NewRequest(Guid? clientId) => InspectionRequest.CreateFromEmail(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new IncomingEmail("42", 1, null, null, "buyer@alnoor.com", "Buyer", "Quote request", [], [], [], [], [], "<id@alnoor.com>", null, null, null),
        clientId: clientId,
        clientName: clientId is null ? null : "Al Noor");

    private static Item ToItem(Guid clientId, MachineCandidate machine)
    {
        var item = Item.Create(clientId, machine.Name);
        item.UpdateDetails(machine.Name, machine.Category, machine.Manufacturer, machine.Model, machine.SerialNumber, machine.Capacity, Domain.Enums.ItemType.Equipment, machine.Location);
        return item;
    }

    private sealed class FakeCatalog(Guid clientId, params Item[] machines) : IEquipmentCatalog
    {
        public IReadOnlyList<Item> Machines { get; } = machines;

        public Task<IReadOnlyList<Item>> ListClientEquipmentAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Item>>(id == clientId ? Machines : []);

        public Task<Client?> FindClientByNameAsync(Guid managerId, string name, CancellationToken cancellationToken = default) => Task.FromResult<Client?>(null);

        public Task<Item?> FindEquipmentAsync(Guid id, string name, string? serialNumber, string? capacity, CancellationToken cancellationToken = default) =>
            Task.FromResult<Item?>(null);

        public Task<IReadOnlyDictionary<Guid, Guid>> GetEquipmentOwnersAsync(IReadOnlyCollection<Guid> equipmentIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(new Dictionary<Guid, Guid>());

        public void Stage(Client client) { }

        public void Stage(Item equipment) { }
    }
}
