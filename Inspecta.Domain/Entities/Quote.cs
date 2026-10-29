using Inspecta.Domain.Enums;
using System.Text.Json;

namespace Inspecta.Domain.Entities;

/// <summary>
/// A quotation for an inspection request. It stays <see cref="QuoteStatus.Open"/> while it is prepared and
/// while it waits for the client (<see cref="SentAt"/> tells which), and is closed by an acceptance or rejection.
/// </summary>
public sealed class Quote
{
    public const int DefaultValidForDays = 14;
    public const int MaxValidForDays = 365;
    public const string DefaultPricingUnit = "Per Visit";
    public const int MaxProposalNumberLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Quote() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public InspectionRequest? Request { get; private set; }
    public string? RequestNumber { get; private set; }
    public int Version { get; private set; }
    public long AmountPiastres { get; private set; }
    public long SubtotalPiastres { get; private set; }
    public int ValidForDays { get; private set; } = DefaultValidForDays;
    public DateTimeOffset ValidUntil { get; private set; }
    public QuoteType Type { get; private set; } = QuoteType.Offer;
    public QuoteStatus Status { get; private set; }
    public string? ClientName { get; private set; }

    /// <summary>Person the offer is addressed to (the "Attention" line of the PDF).</summary>
    public string? ContactName { get; private set; }

    /// <summary>Stable number shown on the cover, kept across revisions (for example <c>QT-2026-1A2B3C</c>).</summary>
    public string? QuotationNumber { get; private set; }
    public string? Message { get; private set; }
    public string? EquipmentSummary { get; private set; }
    public string? ClientReply { get; private set; }
    public string ItemsJson { get; private set; } = "[]";
    public string NotesJson { get; private set; } = "[]";
    public string ScopeJson { get; private set; } = "[]";
    public long? ClientCounterPricePiastres { get; private set; }
    public DateTimeOffset? ClientRespondedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Set when a newer revision (a counter offer) replaced this sent quotation.</summary>
    public DateTimeOffset? SupersededAt { get; private set; }
    public DateTimeOffset? LastReminderAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public long ItemsTotalPiastres => GetItems().Aggregate(0L, (total, item) => checked(total + item.TotalPiastres));

    public bool IsSent => SentAt is not null;

    /// <summary>The latest revision; older sent revisions are kept as history.</summary>
    public bool IsCurrent => SupersededAt is null;

    /// <summary>Items, prices and texts can only change while the quotation is open and not yet sent.</summary>
    public bool IsEditable => Status == QuoteStatus.Open && SentAt is null;

    /// <summary>A sent quotation the client did not answer within its validity period.</summary>
    public bool IsExpired => Status == QuoteStatus.Open && SentAt is not null && ValidUntil < DateTimeOffset.UtcNow;

    /// <summary>Reference printed on the document, for example <c>QT-2026-1A2B3C rev01</c>. Editable while the quote is a draft.</summary>
    public string ProposalNumber => RequestNumber ?? $"{Id.ToString("N")[..8].ToUpperInvariant()} rev{Version:D2}";

    public IReadOnlyList<QuoteItem> GetItems() =>
        JsonSerializer.Deserialize<List<QuoteItem>>(ItemsJson, JsonOptions)
        ?? throw new JsonException("Quote items must be a JSON array.");

    public IReadOnlyList<string> GetNotes() =>
        JsonSerializer.Deserialize<List<string>>(NotesJson, JsonOptions)
        ?? throw new JsonException("Quote notes must be a JSON array.");

    public IReadOnlyList<string> GetScopeOfWork() =>
        JsonSerializer.Deserialize<List<string>>(ScopeJson, JsonOptions)
        ?? throw new JsonException("Quote scope of work must be a JSON array.");

    /// <summary>Items that still need a price before the quotation can be sent.</summary>
    public IReadOnlyList<QuoteItem> GetUnpricedItems() => GetItems().Where(item => !item.IsPriced).ToArray();

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
        DateTimeOffset? createdAt = null,
        string? quotationNumber = null,
        string? contactName = null)
    {
        if (requestId == Guid.Empty || createdBy == Guid.Empty) throw new ArgumentException("Request and creator are required.");
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (amountPiastres < 0) throw new ArgumentOutOfRangeException(nameof(amountPiastres));
        if (subtotalPiastres is < 0) throw new ArgumentOutOfRangeException(nameof(subtotalPiastres));
        if (validUntil <= DateTimeOffset.UtcNow) throw new ArgumentException("Quote expiry must be in the future.", nameof(validUntil));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        var now = createdAt ?? DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var number = string.IsNullOrWhiteSpace(quotationNumber)
            ? $"QT-{now.Year}-{id.ToString("N")[..6].ToUpperInvariant()}"
            : quotationNumber.Trim();
        return new Quote
        {
            Id = id,
            RequestId = requestId,
            Version = version,
            QuotationNumber = number,
            ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim(),
            RequestNumber = string.IsNullOrWhiteSpace(requestNumber)
                ? $"{number} rev{version:D2}"
                : requestNumber.Trim(),
            AmountPiastres = amountPiastres,
            SubtotalPiastres = subtotalPiastres ?? amountPiastres,
            ValidUntil = validUntil,
            Type = type,
            Status = QuoteStatus.Open,
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
        IEnumerable<string>? notes = null,
        IEnumerable<string>? scopeOfWork = null,
        int? validForDays = null,
        string? contactName = null)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(items);
        var quoteItems = items
            .Select(item => QuoteItem.Create(
                item.RequestItemId, item.Type, item.Quantity, item.UnitPricePiastres, item.PricingUnit,
                item.Description, item.PrNumber))
            .ToArray();
        if (quoteItems.Length == 0)
            throw new ArgumentException("A quote must contain at least one item.", nameof(items));
        if (clientName is not null && string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("Client name cannot be empty.", nameof(clientName));
        if (validForDays is < 1 or > MaxValidForDays)
            throw new ArgumentOutOfRangeException(nameof(validForDays), $"Validity must be between 1 and {MaxValidForDays} days.");

        ItemsJson = JsonSerializer.Serialize(quoteItems, JsonOptions);
        AmountPiastres = ItemsTotalPiastres;
        SubtotalPiastres = AmountPiastres;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        EquipmentSummary = equipmentSummary?.Trim();
        if (contactName is not null)
            ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        if (clientName is not null)
            ClientName = clientName.Trim();
        if (notes is not null)
            NotesJson = SerializeTexts(notes);
        if (scopeOfWork is not null)
            ScopeJson = SerializeTexts(scopeOfWork);
        if (validForDays is int days)
        {
            ValidForDays = days;
            ValidUntil = DateTimeOffset.UtcNow.AddDays(days);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Changes the reference printed on the document. Only while the quotation is a draft.</summary>
    public void SetProposalNumber(string proposalNumber)
    {
        EnsureEditable();
        var value = proposalNumber?.Trim();
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Proposal number cannot be empty.", nameof(proposalNumber));
        if (value.Length > MaxProposalNumberLength)
            throw new ArgumentException($"Proposal number cannot be longer than {MaxProposalNumberLength} characters.", nameof(proposalNumber));
        if (value.Any(char.IsControl))
            throw new ArgumentException("Proposal number cannot contain control characters.", nameof(proposalNumber));

        RequestNumber = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks a sent, still open quotation as replaced by a newer revision (a counter offer).</summary>
    public void Supersede()
    {
        if (Status != QuoteStatus.Open || SentAt is null || SupersededAt is not null)
            throw new InvalidOperationException("Only the current open quote that was sent can be replaced by a new revision.");

        SupersededAt = DateTimeOffset.UtcNow;
        UpdatedAt = SupersededAt.Value;
    }

    /// <summary>Proposal number for the next revision: keeps the reference and bumps the trailing <c>revNN</c>.</summary>
    public static string NextRevisionNumber(string previousNumber, int version)
    {
        var suffix = $" rev{version:D2}";
        var match = System.Text.RegularExpressions.Regex.Match(
            previousNumber ?? string.Empty, @"^(?<base>.*?)\s*rev\d+\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var reference = match.Success ? match.Groups["base"].Value.Trim() : (previousNumber ?? string.Empty).Trim();
        return reference.Length == 0 ? suffix.Trim() : reference + suffix;
    }

    /// <summary>Marks the quotation as sent to the client. Every item must be priced.</summary>
    public void Send()
    {
        EnsureEditable();
        if (GetItems().Count == 0)
            throw new InvalidOperationException("A quote must contain at least one item before it is sent.");
        if (GetUnpricedItems().Count > 0)
            throw new InvalidOperationException("Every item must have a price before the quote is sent.");
        if (AmountPiastres != ItemsTotalPiastres)
            throw new InvalidOperationException("Quote total must match its line items.");

        SentAt = DateTimeOffset.UtcNow;
        ValidUntil = SentAt.Value.AddDays(ValidForDays);
        UpdatedAt = SentAt.Value;
    }

    /// <summary>Closes a sent quotation as accepted or rejected.</summary>
    public void RecordClientDecision(QuoteStatus decision, string? note = null)
    {
        if (Status != QuoteStatus.Open || SentAt is null || SupersededAt is not null)
            throw new InvalidOperationException("Only the current open quote that was sent can receive a decision.");
        if (decision is not (QuoteStatus.Accepted or QuoteStatus.Rejected))
            throw new ArgumentException("Decision must be accepted or rejected.", nameof(decision));
        if (decision == QuoteStatus.Rejected && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A rejection reason is required.", nameof(note));

        Status = decision;
        ClientReply = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ClientRespondedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ClientRespondedAt.Value;
    }

    public void RecordReminder(DateTimeOffset? sentAt = null)
    {
        if (Status != QuoteStatus.Open || SentAt is null)
            throw new InvalidOperationException("Only an open quote that was sent can be reminded.");
        LastReminderAt = sentAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = LastReminderAt.Value;
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
            throw new InvalidOperationException("Only an open quote that was not sent can be edited.");
    }

    private static string SerializeTexts(IEnumerable<string> values) =>
        JsonSerializer.Serialize(
            values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray(),
            JsonOptions);
}
