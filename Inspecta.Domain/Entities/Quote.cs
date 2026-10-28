using Inspecta.Domain.Enums;
using System.Text.Json;

namespace Inspecta.Domain.Entities;

public sealed class Quote
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Quote() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public InspectionRequest? Request { get; private set; }
    public string? RequestNumber { get; private set; }
    public int Version { get; private set; }
    public long AmountPiastres { get; private set; }
    public long SubtotalPiastres { get; private set; }
    public DateTimeOffset ValidUntil { get; private set; }
    public QuoteType Type { get; private set; } = QuoteType.Offer;
    public QuoteStatus Status { get; private set; }
    public string? ClientName { get; private set; }
    public string? Message { get; private set; }
    public string? EquipmentSummary { get; private set; }
    public string? ClientReply { get; private set; }
    public string ItemsJson { get; private set; } = "[]";
    public string NotesJson { get; private set; } = "[]";
    public long? ClientCounterPricePiastres { get; private set; }
    public DateTimeOffset? ClientRespondedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? LastReminderAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long ItemsTotalPiastres => GetItems().Aggregate(0L, (total, item) => checked(total + item.TotalPiastres));
    public IReadOnlyList<QuoteItem> GetItems() =>
        JsonSerializer.Deserialize<List<QuoteItem>>(ItemsJson, JsonOptions)
        ?? throw new JsonException("Quote items must be a JSON array.");
    public IReadOnlyList<string> GetNotes() =>
        JsonSerializer.Deserialize<List<string>>(NotesJson, JsonOptions)
        ?? throw new JsonException("Quote notes must be a JSON array.");

    public static Quote Create(
        Guid requestId,
        int version,
        long amountPiastres,
        DateTimeOffset validUntil,
        Guid createdBy,
        QuoteType type = QuoteType.Offer,
        string? requestNumber = null,
        string? clientName = null,
        string? message = null,
        string? equipmentSummary = null,
        long? subtotalPiastres = null,
        DateTimeOffset? createdAt = null)
    {
        if (requestId == Guid.Empty || createdBy == Guid.Empty) throw new ArgumentException("Request and creator are required.");
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (amountPiastres < 0) throw new ArgumentOutOfRangeException(nameof(amountPiastres));
        if (subtotalPiastres is < 0) throw new ArgumentOutOfRangeException(nameof(subtotalPiastres));
        if (validUntil <= DateTimeOffset.UtcNow) throw new ArgumentException("Quote expiry must be in the future.", nameof(validUntil));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new Quote
        {
            Id = Guid.NewGuid(), RequestId = requestId, Version = version,
            RequestNumber = requestNumber?.Trim(),
            AmountPiastres = amountPiastres,
            SubtotalPiastres = subtotalPiastres ?? amountPiastres,
            ValidUntil = validUntil,
            Type = type,
            Status = QuoteStatus.Draft,
            ClientName = clientName?.Trim(),
            Message = message?.Trim(),
            EquipmentSummary = equipmentSummary?.Trim(),
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void SetDraftDetails(
        IEnumerable<QuoteItem> items,
        string? message,
        string? equipmentSummary,
        string? clientName = null,
        IEnumerable<string>? notes = null)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(items);
        var quoteItems = items
            .Select(item => QuoteItem.Create(item.RequestItemId, item.Type, item.Quantity, item.UnitPricePiastres, item.PricingUnit))
            .ToArray();
        if (quoteItems.Length == 0)
            throw new ArgumentException("A quote must contain at least one item.", nameof(items));
        if (clientName is not null && string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("Client name cannot be empty.", nameof(clientName));

        ItemsJson = JsonSerializer.Serialize(quoteItems, JsonOptions);
        AmountPiastres = ItemsTotalPiastres;
        SubtotalPiastres = AmountPiastres;
        Message = message?.Trim();
        EquipmentSummary = equipmentSummary?.Trim();
        if (clientName is not null)
            ClientName = clientName.Trim();

        if (notes is not null)
        {
            var noteValues = notes.Where(note => !string.IsNullOrWhiteSpace(note))
                .Select(note => note.Trim())
                .ToArray();
            NotesJson = JsonSerializer.Serialize(noteValues, JsonOptions);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Send()
    {
        EnsureDraft();
        if (GetItems().Count > 0 && AmountPiastres != ItemsTotalPiastres)
            throw new InvalidOperationException("Quote total must match its line items.");
        Status = QuoteStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        UpdatedAt = SentAt.Value;
    }

    public void RecordClientDecision(QuoteStatus decision, string? note = null)
    {
        if (Status != QuoteStatus.Sent) throw new InvalidOperationException("Only sent quotes can receive a client decision.");
        if (decision is not (QuoteStatus.Countered or QuoteStatus.Accepted or QuoteStatus.Rejected))
            throw new ArgumentException("Decision must be countered, accepted, or rejected.", nameof(decision));
        if (decision == QuoteStatus.Rejected && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A rejection reason is required.", nameof(note));
        Status = decision;
        ClientReply = note?.Trim();
        ClientRespondedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ClientRespondedAt.Value;
    }

    public void RecordClientResponse(QuoteStatus outcome, string? note, long? counterPricePiastres, DateTimeOffset? respondedAt = null)
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException("Only sent quotes can receive a client decision.");
        if (outcome is not (QuoteStatus.Countered or QuoteStatus.Accepted or QuoteStatus.Rejected))
            throw new ArgumentException("Outcome must be countered, accepted, or rejected.", nameof(outcome));
        if (outcome == QuoteStatus.Rejected && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A rejection reason is required.", nameof(note));
        if (counterPricePiastres is < 0)
            throw new ArgumentOutOfRangeException(nameof(counterPricePiastres));
        if (outcome == QuoteStatus.Countered && counterPricePiastres is null)
            throw new ArgumentException("A counter price is required for a counter offer.", nameof(counterPricePiastres));

        Status = outcome;
        ClientReply = note?.Trim();
        ClientCounterPricePiastres = counterPricePiastres;
        ClientRespondedAt = respondedAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = ClientRespondedAt.Value;
    }

    public void RecordReminder(DateTimeOffset? sentAt = null)
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException("Only sent quotes can be reminded.");
        LastReminderAt = sentAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = LastReminderAt.Value;
    }

    public void Supersede()
    {
        if (Status is not (QuoteStatus.Sent or QuoteStatus.Countered or QuoteStatus.Accepted))
            throw new InvalidOperationException("Only active quotes can be superseded.");
        Status = QuoteStatus.Superseded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Only draft quotes can be edited.");
    }
}