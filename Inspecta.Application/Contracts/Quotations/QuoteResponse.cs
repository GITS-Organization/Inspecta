using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Quote revision and current client decision.</summary>
public sealed record QuoteResponse(Guid Id, Guid RequestId, int Version, long AmountPiastres, DateTimeOffset ValidUntil, QuoteStatus Status, string? ClientReply, DateTimeOffset CreatedAt);