using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class CertificateService(
    ICertificateRepository certificateRepository,
    IRequestRepository requestRepository) : ICertificateService
{
    public async Task<IReadOnlyList<CertificateResponse>?> ListAsync(
        Guid requestId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !CanAccess(request, actorId, role)) return null;
        return (await certificateRepository.ListForRequestAsync(requestId, cancellationToken))
            .Select(CertificateResponseMapper.Map)
            .ToArray();
    }

    public async Task<CertificateResponse?> GetAsync(
        Guid requestId,
        Guid requestItemId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !CanAccess(request, actorId, role)) return null;
        EnsureRequestItemExists(request, requestItemId);
        var certificate = await certificateRepository.GetByRequestItemIdAsync(
            requestId, requestItemId, tracking: false, cancellationToken);
        return certificate is null ? null : CertificateResponseMapper.Map(certificate);
    }

    public async Task<CertificateResponse> SaveDraftAsync(
        Guid requestId,
        Guid requestItemId,
        Guid actorId,
        string role,
        SaveCertificateDraftRequest draft,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        if (!CanEdit(request, actorId, role))
            throw new AuthException("forbidden", "Only the assigned inspector can edit this certificate.", 403);
        if (request.Status is not (JobStatus.InProgress or JobStatus.CertificateReturned))
            throw new InvalidOperationException("Certificate drafts can only be edited during inspection or after return.");

        var requestItem = EnsureRequestItemExists(request, requestItemId);
        var certificate = await certificateRepository.GetByRequestItemIdAsync(
            requestId, requestItemId, tracking: true, cancellationToken);
        if (certificate is null)
        {
            certificate = InspectionCertificate.Create(
                requestId,
                request.InspectorId ?? actorId,
                equipmentId: requestItem.EquipmentId,
                requestItemId: requestItemId);
            certificate.SetDraft(draft.Texts, draft.Dates, draft.Answers, draft.FunctionCheck, draft.FinalResult);
            await certificateRepository.AddAsync(certificate, cancellationToken);
        }
        else
        {
            if (role == "inspector" && certificate.InspectorId != actorId)
                throw new AuthException("forbidden", "Certificate belongs to another inspector.", 403);
            certificate.SetDraft(draft.Texts, draft.Dates, draft.Answers, draft.FunctionCheck, draft.FinalResult);
            await certificateRepository.SaveAsync(cancellationToken);
        }

        return CertificateResponseMapper.Map(certificate);
    }

    public async Task<CertificateResponse> SubmitAsync(
        Guid requestId,
        Guid requestItemId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        if (!CanEdit(request, actorId, role))
            throw new AuthException("forbidden", "Only the assigned inspector can submit this certificate.", 403);
        EnsureRequestItemExists(request, requestItemId);
        var certificate = await certificateRepository.GetByRequestItemIdAsync(
            requestId, requestItemId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Save a certificate draft before submitting.");

        certificate.Submit();
        if (request.Items.Count > 0 &&
            request.Items.All(item => request.Certificates.Any(existing =>
                existing.RequestItemId == item.Id && existing.SubmittedAt is not null)))
        {
            if (request.Status == JobStatus.CertificateReturned)
                request.TransitionTo(JobStatus.InProgress, actorId);
            if (request.Status == JobStatus.InProgress)
                request.TransitionTo(JobStatus.CertificateSubmitted, actorId);
        }

        await certificateRepository.SaveAsync(cancellationToken);
        return CertificateResponseMapper.Map(certificate);
    }

    public async Task<CertificateResponse> ReviewAsync(
        Guid requestId,
        Guid requestItemId,
        Guid actorId,
        ReviewCertificateRequest review,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        EnsureRequestItemExists(request, requestItemId);
        if (request.Status is not (JobStatus.CertificateSubmitted or JobStatus.CertificateReturned))
            throw new InvalidOperationException("Certificates can only be reviewed after all request items are submitted.");

        var certificate = await certificateRepository.GetByRequestItemIdAsync(
            requestId, requestItemId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        if (review.Approve)
        {
            certificate.Approve(
                actorId,
                review.CertificateNumber ?? string.Empty,
                review.VerificationCode ?? string.Empty,
                review.SentTo ?? string.Empty,
                review.SignatureStrokesJson ?? "[]");
            if (request.Items.Count > 0 &&
                request.Items.All(item => request.Certificates.Any(existing =>
                    existing.RequestItemId == item.Id && existing.IsApproved)))
            {
                request.TransitionTo(JobStatus.CertificateApproved, actorId, review.Note);
            }
        }
        else
        {
            certificate.ReturnForChanges(actorId, review.Note ?? string.Empty);
            if (request.Status == JobStatus.CertificateSubmitted)
                request.TransitionTo(JobStatus.CertificateReturned, actorId, review.Note);
        }

        await certificateRepository.SaveAsync(cancellationToken);
        return CertificateResponseMapper.Map(certificate);
    }

    private static RequestItem EnsureRequestItemExists(InspectionRequest request, Guid requestItemId) =>
        request.Items.SingleOrDefault(item => item.Id == requestItemId)
        ?? throw new KeyNotFoundException("Request item was not found.");

    private static bool CanAccess(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "coordinator" or "technical_manager" ||
        (role == "inspector" && request.InspectorId == actorId);

    private static bool CanEdit(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "technical_manager" ||
        (role == "inspector" && request.InspectorId == actorId);
}
