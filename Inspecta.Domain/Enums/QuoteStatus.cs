namespace Inspecta.Domain.Enums;

/// <summary>
/// A quotation is either still open (being prepared, or sent and waiting for the client) or closed by
/// an acceptance or a rejection. Whether it was sent is tracked by <c>SentAt</c>.
/// </summary>
public enum QuoteStatus
{
    Open,
    Accepted,
    Rejected
}
