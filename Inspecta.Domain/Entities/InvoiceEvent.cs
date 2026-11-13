using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

/// <summary>One step in the life of an invoice (created, emailed, handed over, closed). Only ever added.</summary>
public sealed class InvoiceEvent
{
    private InvoiceEvent() { }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid ManagerId { get; private set; }
    public InvoiceEventKind Kind { get; private set; }
    public Guid? ActorId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Note { get; private set; }

    internal static InvoiceEvent Create(Invoice invoice, InvoiceEventKind kind, Guid actorId, string? note = null) => new()
    {
        Id = Guid.NewGuid(),
        InvoiceId = invoice.Id,
        ManagerId = invoice.ManagerId,
        Kind = kind,
        ActorId = actorId,
        At = DateTimeOffset.UtcNow,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
    };
}
