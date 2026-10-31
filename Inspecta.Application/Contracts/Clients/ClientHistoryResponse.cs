using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Contracts.Clients;

/// <summary>Historical inspection requests and their certificates for a client.</summary>
public sealed record ClientHistoryResponse(
    Guid ClientId,
    IReadOnlyList<ClientRequestHistoryResponse> Requests);

/// <summary>
/// A previous client request together with all certificates created for it. <c>issuedCertificates</c> are the approved ones as
/// issued (number, result, QR, and when and to whom they were sent); open them with <c>/clients/{clientId}/certificates/{id}/pdf</c>.
/// </summary>
public sealed record ClientRequestHistoryResponse(
    RequestResponse Request,
    IReadOnlyList<CertificateResponse> Certificates,
    IReadOnlyList<ClientCertificateResponse>? IssuedCertificates = null);
