using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>Reads and replaces what a company says about its quotations (terms, bank accounts, contacts).</summary>
public interface IQuotationContentService
{
    /// <summary>What the company saved, or the default when it saved nothing (the built-in content for the built-in company, empty lists otherwise).</summary>
    Task<QuotationContentResponse> GetAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the whole content. Anything over a limit throws <see cref="ArgumentException"/> and saves nothing.</summary>
    Task<QuotationContentResponse> SaveAsync(Guid companyId, SaveQuotationContentRequest request, Guid actorId, CancellationToken cancellationToken = default);
}

/// <summary>What a quotation prints from the company and the coordinator, for the PDF and for the snapshot kept when it is sent.</summary>
public interface IQuotationContentProvider
{
    /// <summary>
    /// What the PDF of the quotation prints. A draft prints the live content; a quotation that was sent prints what it was sent with, and
    /// <c>null</c> when it was sent before that was kept (it then prints as it always did).
    /// </summary>
    Task<QuotationContentSnapshot?> ForQuoteAsync(
        Quote quote, InspectionRequest request, AppUser? coordinator, CompanyBranding? branding, CancellationToken cancellationToken = default);

    /// <summary>The live content now, for a quotation that is about to be sent: the caller draws the PDF from it and stores it with the quotation.</summary>
    Task<QuotationContentSnapshot> CaptureAsync(
        InspectionRequest request, AppUser? coordinator, CompanyBranding? branding, CancellationToken cancellationToken = default);
}
