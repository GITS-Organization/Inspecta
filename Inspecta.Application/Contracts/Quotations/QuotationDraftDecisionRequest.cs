using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>A decision made by the coordinator responsible for the quotation draft.</summary>
public sealed record QuotationDraftDecisionRequest(QuotationDraftStatus Status);
