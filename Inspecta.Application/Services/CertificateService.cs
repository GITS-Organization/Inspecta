using System.Text.Json;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class CertificateService(ICertificateRepository certificateRepository, IRequestRepository requestRepository) : ICertificateService
{
    public async Task<CertificateResponse?> GetAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !CanAccess(request, actorId, role)) return null;
        var certificate = await certificateRepository.GetByRequestIdAsync(requestId, tracking: false, cancellationToken);
        return certificate is null ? null : Map(certificate);
    }

    public async Task<CertificateResponse> SaveDraftAsync(Guid requestId, Guid actorId, SaveCertificateDraftRequest draft, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        if (request.InspectorId != actorId) throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Only the assigned inspector can edit this certificate.", 403);
        if (request.Status is not (JobStatus.InProgress or JobStatus.CertificateReturned))
            throw new InvalidOperationException("Certificate drafts can only be edited during inspection or after return.");

        var certificate = await certificateRepository.GetByRequestIdAsync(requestId, tracking: true, cancellationToken);
        if (certificate is null)
        {
            certificate = InspectionCertificate.Create(requestId, actorId);
            certificate.SetDraft(draft.Texts, draft.Dates, draft.Answers, draft.FunctionCheck, draft.FinalResult);
            await certificateRepository.AddAsync(certificate, cancellationToken);
        }
        else
        {
            if (certificate.InspectorId != actorId) throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Certificate belongs to another inspector.", 403);
            certificate.SetDraft(draft.Texts, draft.Dates, draft.Answers, draft.FunctionCheck, draft.FinalResult);
            await certificateRepository.SaveAsync(cancellationToken);
        }

        return Map(certificate);
    }

    public async Task<CertificateResponse> SubmitAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        if (request.InspectorId != actorId) throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Only the assigned inspector can submit this certificate.", 403);
        var certificate = await certificateRepository.GetByRequestIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Save a certificate draft before submitting.");
        certificate.Submit();
        if (request.Status == JobStatus.CertificateReturned) request.TransitionTo(JobStatus.InProgress, actorId);
        request.TransitionTo(JobStatus.CertificateSubmitted, actorId);
        await certificateRepository.SaveAsync(cancellationToken);
        return Map(certificate);
    }

    public async Task<CertificateResponse> ReviewAsync(Guid requestId, Guid actorId, ReviewCertificateRequest review, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        var certificate = await certificateRepository.GetByRequestIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        if (review.Approve)
        {
            certificate.Approve(actorId, review.CertificateNumber ?? string.Empty, review.VerificationCode ?? string.Empty,
                review.SentTo ?? string.Empty, review.SignatureStrokesJson ?? "[]");
            request.TransitionTo(JobStatus.CertificateApproved, actorId, review.Note);
        }
        else
        {
            certificate.ReturnForChanges(actorId, review.Note ?? string.Empty);
            request.TransitionTo(JobStatus.CertificateReturned, actorId, review.Note);
        }
        await certificateRepository.SaveAsync(cancellationToken);
        return Map(certificate);
    }

    private static bool CanAccess(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "coordinator" or "technical_manager" || (role == "inspector" && request.InspectorId == actorId);

    private static CertificateResponse Map(InspectionCertificate certificate) => new(
        certificate.RequestId,
        certificate.InspectorId,
        certificate.TemplateId,
        JsonSerializer.Deserialize<Dictionary<string, string>>(certificate.TextsJson) ?? [],
        JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(certificate.DatesJson) ?? [],
        JsonSerializer.Deserialize<Dictionary<string, bool>>(certificate.AnswersJson) ?? [],
        certificate.FunctionCheck,
        certificate.FinalResult,
        certificate.SubmittedAt,
        certificate.ReviewNote,
        certificate.CertificateNumber,
        certificate.VerificationCode,
        certificate.ReviewedAt);
}