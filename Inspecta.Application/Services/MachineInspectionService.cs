using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class MachineInspectionService(
    ICertificateRepository certificateRepository,
    IRequestRepository requestRepository,
    IUserRepository userRepository,
    IEquipmentRepository equipmentRepository,
    IClientRepository clientRepository,
    IPdfGenerator pdfGenerator,
    ICertificateFileRepository certificateFiles,
    CertificateOptions? options = null,
    CertificateDeliveryService? deliveryService = null,
    IQrCodeRenderer? qrCodes = null) : IMachineInspectionService
{
    private const int MaxTextLength = 4000;

    // ---- Inspector: start and fill in ---------------------------------------------------------------------------

    public async Task<MachineListResponse> StartInspectionAsync(
        Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        if (!CanEdit(request, actorId, role))
            throw new AuthException("forbidden", "Only the assigned inspector can start this inspection.", 403);

        if (request.Status is JobStatus.Assigned)
            request.TransitionTo(JobStatus.TaskAccepted, actorId);
        if (request.Status is JobStatus.TaskAccepted)
            request.TransitionTo(JobStatus.InProgress, actorId);
        else if (request.Status is not (JobStatus.InProgress or JobStatus.CertificateReturned))
            throw new InvalidOperationException("The inspection can only start once the job is assigned.");

        await requestRepository.SaveAsync(request, cancellationToken);
        return await BuildListAsync(request, actorId, role, cancellationToken);
    }

    public async Task<MachineListResponse> ListMachinesAsync(
        Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        return await BuildListAsync(request, actorId, role, cancellationToken);
    }

    public async Task<MachineFormResponse> GetFormAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        var item = FindItem(request, requestItemId);
        var certificate = request.Certificates.SingleOrDefault(value => value.RequestItemId == requestItemId);
        EnsureTemplateCertificate(certificate);
        return await BuildFormAsync(request, item, certificate, actorId, role, cancellationToken);
    }

    public async Task<MachineFormResponse> SaveFormAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, SaveMachineFormRequest form,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        if (!CanEdit(request, actorId, role))
            throw new AuthException("forbidden", "Only the assigned inspector can edit this certificate.", 403);
        if (request.Status is not (JobStatus.InProgress or JobStatus.CertificateReturned))
            throw new AuthException("inspection_not_started", "Start the inspection before filling the certificates.", 409);

        var item = FindItem(request, requestItemId);
        var (match, equipment) = await ResolveAsync(item, cancellationToken);
        var template = match.Template;

        var certificate = await certificateRepository.GetByRequestItemIdAsync(requestId, requestItemId, tracking: true, cancellationToken);
        EnsureTemplateCertificate(certificate);
        var isNew = certificate is null;
        certificate ??= InspectionCertificate.CreateForTemplate(
            requestId, request.InspectorId ?? actorId, requestItemId, item.EquipmentId, template.Key, template.Version);
        if (role == "inspector" && certificate.InspectorId != actorId)
            throw new AuthException("forbidden", "This certificate belongs to another inspector.", 403);
        if (StatusOf(isNew ? null : certificate) is MachineStatus.Submitted or MachineStatus.Approved or MachineStatus.Delivered)
            throw new AuthException("certificate_locked", "A submitted certificate cannot be edited; wait for the review or for it to be returned.", 409);
        if (!isNew) template = CertificateTemplateCatalog.Find(certificate.TemplateId) ?? template;

        var technical = Merge(certificate.Technical, ReadFields(form.Fields, template));
        var checklist = Merge(certificate.Checklist, ReadChecklist(form.Checklist, template));
        var texts = Merge(certificate.TextsOf(), ReadTexts(form.Texts));
        var dates = Merge(certificate.DatesOf(), ReadDates(form.Dates));
        var answers = Merge(certificate.AnswersOf(), ReadAnswers(form.Answers));
        if (answers.TryGetValue("within6Months", out var six) && !(form.Answers?.ContainsKey("within12Months") ?? false))
            answers["within12Months"] = !six;
        if (answers.TryGetValue("within12Months", out var twelve) && !(form.Answers?.ContainsKey("within6Months") ?? false))
            answers["within6Months"] = !twelve;
        if (answers.TryGetValue("within6Months", out var a6) && answers.TryGetValue("within12Months", out var a12) && a6 == a12)
            throw new ArgumentException("Within 6 months and within 12 months must be opposite answers.");
        if (!answers.GetValueOrDefault("firstExamination")) answers.Remove("installedCorrectly");
        if (!answers.GetValueOrDefault("futureDanger")) dates.Remove("futureDangerBy");

        certificate.SaveForm(technical, checklist, texts, dates, answers);
        if (isNew) await certificateRepository.AddAsync(certificate, cancellationToken);
        else await certificateRepository.SaveAsync(cancellationToken);

        return await BuildFormAsync(request, item, certificate, actorId, role, cancellationToken, equipment);
    }

    public async Task<MachineFormResponse> SubmitAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        if (!CanEdit(request, actorId, role))
            throw new AuthException("forbidden", "Only the assigned inspector can submit this certificate.", 403);
        if (request.Status is not (JobStatus.InProgress or JobStatus.CertificateReturned))
            throw new AuthException("inspection_not_started", "Start the inspection before submitting.", 409);

        var item = FindItem(request, requestItemId);
        var certificate = await certificateRepository.GetByRequestItemIdAsync(requestId, requestItemId, tracking: true, cancellationToken)
            ?? throw new AuthException("nothing_to_submit", "Fill the certificate before submitting it.", 409);
        EnsureTemplateCertificate(certificate);
        var template = CertificateTemplateCatalog.Find(certificate.TemplateId) ?? CertificateTemplateCatalog.Basic;

        var texts = certificate.TextsOf();
        var problems = MachineFormRules.Problems(template, certificate.Checklist, texts, certificate.DatesOf(), certificate.AnswersOf());
        if (problems.Count > 0)
            throw new AuthException("certificate_incomplete", $"Complete the certificate first. Missing: {string.Join("; ", problems)}.", 409) { Problems = problems };

        var (match, equipment) = await ResolveAsync(item, cancellationToken);
        var finalTexts = MachineFormRules.WithDefaults(texts);
        if (!finalTexts.ContainsKey("standardOfInspection"))
            finalTexts["standardOfInspection"] = string.Join("\n", StandardsOf(match, equipment));
        certificate.SaveForm(certificate.Technical, certificate.Checklist, finalTexts, certificate.DatesOf(), certificate.AnswersOf());

        var inspector = await userRepository.GetByIdAsync(certificate.InspectorId, cancellationToken);
        var preparedBy = inspector?.FullName ?? "Inspector";
        var qualifications = inspector is null ? null : string.Join(", ", inspector.Qualifications.Select(value => value.Name));
        certificate.SubmitForm(preparedBy, qualifications);

        if (request.Items.Count > 0 && request.Items.All(value => request.Certificates.Any(existing =>
                existing.RequestItemId == value.Id &&
                (existing.IsApproved || existing is { SubmittedAt: not null, ReviewedAt: null }))))
        {
            if (request.Status == JobStatus.CertificateReturned)
                request.TransitionTo(JobStatus.InProgress, actorId);
            if (request.Status == JobStatus.InProgress)
                request.TransitionTo(JobStatus.CertificateSubmitted, actorId);
        }

        await requestRepository.SaveAsync(request, cancellationToken);
        return await BuildFormAsync(request, item, certificate, actorId, role, cancellationToken, equipment);
    }

    // ---- Technical manager: review ------------------------------------------------------------------------------

    public async Task<MachineFormResponse> ApproveAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, ApproveMachineRequest? approval = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReviewer(role);
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        EnsureUnderReview(request);
        var item = FindItem(request, requestItemId);
        var certificate = await certificateRepository.GetByRequestItemIdAsync(requestId, requestItemId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        EnsureTemplateCertificate(certificate);
        if (certificate.SubmittedAt is null || certificate.ReviewedAt is not null)
            throw new AuthException("not_submitted", "Only a submitted certificate can be approved.", 409);

        var reviewer = await userRepository.GetByIdAsync(actorId, cancellationToken);
        var signature = !string.IsNullOrWhiteSpace(approval?.Signature) ? approval.Signature : reviewer?.SignatureStrokesJson;
        var number = await certificateFiles.NextCertificateNumberAsync(request.ManagerId ?? Guid.Empty, DateTimeOffset.UtcNow.Year, cancellationToken);
        var code = NewVerificationCode();
        certificate.ApproveForm(
            actorId, reviewer?.FullName ?? string.Empty, number, code, (options ?? new CertificateOptions()).VerifyUrl(code), signature);
        if (approval is { RememberSignature: true } && !string.IsNullOrWhiteSpace(approval.Signature))
            reviewer?.SetSignature(approval.Signature);

        if (certificate.EquipmentId is Guid equipmentId &&
            certificate.DatesOf() is { } dates &&
            dates.TryGetValue("examination", out var examination) && dates.TryGetValue("nextExamination", out var next))
        {
            // The registered machine remembers when it was last inspected and when it is due again.
            var registered = await equipmentRepository.GetByIdAsync(equipmentId, tracking: true, cancellationToken);
            if (registered is { IsArchived: false } && next > examination)
            {
                try { registered.RecordInspection(examination, next); }
                catch (InvalidOperationException) { /* a retired machine keeps its old dates */ }
            }
        }

        if (request.Items.Count > 0 && request.Items.All(value =>
                request.Certificates.Any(existing => existing.RequestItemId == value.Id && existing.IsApproved)))
            request.TransitionTo(JobStatus.CertificateApproved, actorId);

        // The PDF is drawn once, now, and filed under the client exactly as issued.
        var (match, equipment) = await ResolveAsync(item, cancellationToken);
        var template = CertificateTemplateCatalog.Find(certificate.TemplateId) ?? match.Template;
        var client = await FindClientAsync(request, cancellationToken);
        var inspector = await userRepository.GetByIdAsync(certificate.InspectorId, cancellationToken);
        var previous = await PreviousCertificateAsync(certificate, cancellationToken);
        var pdf = pdfGenerator.Generate(BuildPdfModel(request, item, certificate, template, match, equipment, client, inspector, previous));
        var examined = certificate.DatesOf();
        await certificateFiles.AddAsync(
            IssuedCertificateFile.Create(
                certificate.Id,
                request.Id,
                request.ManagerId ?? Guid.Empty,
                request.ClientId,
                request.ClientName,
                match.Item?.Name ?? item.Name,
                number,
                code,
                examined.TryGetValue("examination", out var examinedOn) ? DateOnly.FromDateTime(examinedOn.UtcDateTime) : null,
                examined.TryGetValue("nextExamination", out var nextOn) ? DateOnly.FromDateTime(nextOn.UtcDateTime) : null,
                pdf.FileName,
                pdf.Content,
                equipmentId: certificate.EquipmentId,
                qrData: certificate.QrData,
                preparedBy: certificate.TextsOf().GetValueOrDefault("preparedByName")),
            cancellationToken);

        await requestRepository.SaveAsync(request, cancellationToken);
        return await BuildFormAsync(request, item, certificate, actorId, role, cancellationToken);
    }

    public async Task<MachineFormResponse> ReturnAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, string note, CancellationToken cancellationToken = default)
    {
        EnsureReviewer(role);
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A note explaining what to fix is required.", nameof(note));
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        EnsureUnderReview(request);
        var item = FindItem(request, requestItemId);
        var certificate = await certificateRepository.GetByRequestItemIdAsync(requestId, requestItemId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        EnsureTemplateCertificate(certificate);
        if (certificate.SubmittedAt is null || certificate.ReviewedAt is not null)
            throw new AuthException("not_submitted", "Only a submitted certificate can be returned.", 409);

        var reviewer = await userRepository.GetByIdAsync(actorId, cancellationToken);
        certificate.ReturnForChanges(actorId, note, reviewer?.FullName);
        if (request.Status == JobStatus.CertificateSubmitted)
            request.TransitionTo(JobStatus.CertificateReturned, actorId, note);

        await requestRepository.SaveAsync(request, cancellationToken);
        return await BuildFormAsync(request, item, certificate, actorId, role, cancellationToken);
    }

    /// <summary>Approves every machine of the job that is waiting for review, one after the other.</summary>
    public async Task<MachineListResponse> ApproveAllAsync(
        Guid requestId, Guid actorId, string role, ApproveMachineRequest? approval = null, CancellationToken cancellationToken = default)
    {
        EnsureReviewer(role);
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        EnsureUnderReview(request);
        var waiting = WaitingForReview(request);
        if (waiting.Count == 0)
            throw new AuthException("nothing_to_review", "No certificate of this job is waiting for review.", 409);

        // Each machine gets its own number, PDF and save: if one fails the others stay approved and calling again finishes the rest.
        foreach (var requestItemId in waiting)
            await ApproveAsync(requestId, requestItemId, actorId, role, approval, cancellationToken);
        return await ListMachinesAsync(requestId, actorId, role, cancellationToken);
    }

    /// <summary>Sends the job back to the inspector: every machine waiting for review, or only the ones named, with the same note.</summary>
    public async Task<MachineListResponse> ReturnToInspectorAsync(
        Guid requestId, Guid actorId, string role, ReturnJobRequest returned, CancellationToken cancellationToken = default)
    {
        EnsureReviewer(role);
        if (string.IsNullOrWhiteSpace(returned.Note)) throw new ArgumentException("A note explaining what to fix is required.", nameof(returned));
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        EnsureUnderReview(request);
        var waiting = WaitingForReview(request);
        var chosen = returned.RequestItemIds is { Count: > 0 } ids ? ids.Distinct().ToArray() : waiting.ToArray();
        foreach (var requestItemId in chosen)
        {
            FindItem(request, requestItemId);
            if (!waiting.Contains(requestItemId))
                throw new AuthException("not_submitted", "Only machines waiting for review can be sent back.", 409);
        }

        if (chosen.Length == 0)
            throw new AuthException("nothing_to_review", "No certificate of this job is waiting for review.", 409);
        foreach (var requestItemId in chosen)
            await ReturnAsync(requestId, requestItemId, actorId, role, returned.Note, cancellationToken);
        return await ListMachinesAsync(requestId, actorId, role, cancellationToken);
    }

    private static IReadOnlyList<Guid> WaitingForReview(InspectionRequest request) =>
        request.Items
            .Where(item => StatusOf(request.Certificates.SingleOrDefault(value => value.RequestItemId == item.Id)) == MachineStatus.Submitted)
            .Select(item => item.Id)
            .ToArray();

    // ---- PDF, QR and delivery -----------------------------------------------------------------------------------

    /// <summary>
    /// Every certificate of the job in one ZIP, as it will be printed, so the technical manager can look at them all before
    /// approving: drafts carry the DRAFT mark, approved ones are the stored final files. Machines not saved yet are left out.
    /// </summary>
    public async Task<DownloadFile> GetPreviewZipAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        if (role == "coordinator")
            throw new AuthException("forbidden", "The coordinator gets the certificates once they are approved (certificates.zip).", 403);

        var entries = new List<(string Name, byte[] Content)>();
        var number = 0;
        foreach (var item in request.Items.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
        {
            number++;
            if (request.Certificates.SingleOrDefault(value => value.RequestItemId == item.Id) is not { UsesTemplate: true }) continue;
            var pdf = await GetPdfAsync(requestId, item.Id, actorId, role, cancellationToken);
            entries.Add(($"{number:00}-{SafeName(item.Name)}.pdf", pdf.Content));
        }

        if (entries.Count == 0) throw new KeyNotFoundException("No certificate of this job has been saved yet.");
        return new DownloadFile(CertificateDeliveryService.Zip([], entries), $"preview-{SafeName(request.ClientName)}.zip", "application/zip");
    }

    /// <summary>The QR code of an approved certificate (the one printed on it) as a PNG, to send to the client.</summary>
    public async Task<DownloadFile> GetQrCodeAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        FindItem(request, requestItemId);
        var certificate = request.Certificates.SingleOrDefault(value => value.RequestItemId == requestItemId)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        if (!certificate.IsApproved || string.IsNullOrWhiteSpace(certificate.QrData))
            throw new AuthException("not_approved", "A certificate gets its QR code when it is approved.", 409);
        return QrFile(certificate.QrData, certificate.CertificateNumber);
    }

    private DownloadFile QrFile(string qrData, string? certificateNumber) =>
        new((qrCodes ?? throw new InvalidOperationException("QR codes are not available.")).RenderPng(qrData),
            $"qr-{SafeName(certificateNumber ?? "certificate")}.png", "image/png");

    public async Task<PdfFile> GetPdfAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        var item = FindItem(request, requestItemId);
        var certificate = request.Certificates.SingleOrDefault(value => value.RequestItemId == requestItemId)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        EnsureTemplateCertificate(certificate);
        if (role == "coordinator" && !certificate.IsApproved)
            throw new AuthException("not_approved", "The certificate is available to the coordinator once it is approved.", 409);
        // An approved certificate is served exactly as it was issued.
        if (certificate.IsApproved && await certificateFiles.GetByCertificateIdAsync(certificate.Id, cancellationToken) is { } issued)
            return new PdfFile(issued.Content, issued.FileName);

        var (match, equipment) = await ResolveAsync(item, cancellationToken);
        var template = CertificateTemplateCatalog.Find(certificate.TemplateId) ?? match.Template;
        var client = await FindClientAsync(request, cancellationToken);
        var inspector = await userRepository.GetByIdAsync(certificate.InspectorId, cancellationToken);
        var previous = await PreviousCertificateAsync(certificate, cancellationToken);
        return pdfGenerator.Generate(BuildPdfModel(request, item, certificate, template, match, equipment, client, inspector, previous));
    }

    public async Task<MachineListResponse> DeliverAsync(
        Guid requestId, Guid actorId, string role, DeliverCertificatesRequest delivery, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        if (role == "inspector")
            throw new AuthException("forbidden", "Inspectors cannot deliver certificates.", 403);
        if (role == "coordinator" && !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can deliver its certificates.", 403);
        if (request.Status != JobStatus.CertificateApproved)
            throw new AuthException("not_approved", "Every certificate of the job must be approved before it is delivered.", 409);

        var client = await FindClientAsync(request, cancellationToken);
        var files = await certificateFiles.ListForRequestAsync(request.Id, cancellationToken);
        // The email goes first: if the mail server refuses it nothing below changes and the call can be repeated.
        var sent = deliveryService is null
            ? new CertificateDeliveryResult(false, null, "Email delivery is not available, so no email was sent. The certificates are marked as handed over.")
            : await deliveryService.DeliverAsync(request, files, delivery.SentTo, delivery.Message, cancellationToken);
        var sentTo = sent.SentTo ?? (string.IsNullOrWhiteSpace(delivery.SentTo) ? client?.CertificatesEmail : delivery.SentTo);
        foreach (var certificate in request.Certificates.Where(value => value.IsApproved && !value.IsDelivered))
            certificate.MarkDelivered(sentTo);
        request.TransitionTo(JobStatus.SentToClient, actorId, string.IsNullOrWhiteSpace(sentTo) ? null : $"Delivered to {sentTo}");

        await requestRepository.SaveAsync(request, cancellationToken);
        return (await BuildListAsync(request, actorId, role, cancellationToken)) with
        {
            Delivery = new MachineDelivery(sent.EmailSent, sentTo, sent.Notice)
        };
    }

    public async Task<DeliveryPreviewResponse> PreviewDeliveryAsync(
        Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadDeliverableAsync(requestId, actorId, role, cancellationToken);
        var files = await certificateFiles.ListForRequestAsync(request.Id, cancellationToken);
        if (deliveryService is null)
            return new DeliveryPreviewResponse(false, null, null, string.Empty, string.Empty, files.Select(file => file.FileName).ToArray(), false, "Email delivery is not available.");
        var plan = await deliveryService.PlanAsync(request, files, null, null, cancellationToken);
        var notice = !deliveryService.Enabled
            ? "Email delivery is switched off: handing the certificates over will not send this email."
            : plan.ToAddress is null ? "There is no address to email: the request has no source email and the client has no certificates email." : null;
        return new DeliveryPreviewResponse(
            deliveryService.Enabled, plan.ToAddress, plan.ToName, plan.Subject, plan.Body, plan.AttachmentNames, plan.RepliesToOriginalEmail, notice);
    }

    public async Task<DownloadFile> GetCertificatesZipAsync(
        Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadDeliverableAsync(requestId, actorId, role, cancellationToken);
        var files = await certificateFiles.ListForRequestAsync(request.Id, cancellationToken);
        if (files.Count == 0) throw new KeyNotFoundException("The job has no issued certificates.");
        // Each certificate's QR code goes next to the PDFs, under qr/.
        var qrImages = qrCodes is null
            ? []
            : files.Where(file => !string.IsNullOrWhiteSpace(file.QrData))
                .Select(file => ($"qr/{Path.GetFileNameWithoutExtension(file.FileName)}.png", qrCodes.RenderPng(file.QrData!)))
                .ToArray();
        return new DownloadFile(CertificateDeliveryService.Zip(files, qrImages), $"certificates-{SafeName(request.ClientName)}.zip", "application/zip");
    }

    // The job's approved certificates are for the coordinator side (owner, technical manager, Admin) once every machine is approved.
    private async Task<InspectionRequest> LoadDeliverableAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        if (role == "inspector")
            throw new AuthException("forbidden", "Inspectors cannot hand over certificates.", 403);
        if (role == "coordinator" && !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can do this.", 403);
        if (request.Status is not (JobStatus.CertificateApproved or JobStatus.SentToClient))
            throw new AuthException("not_approved", "Every certificate of the job must be approved first.", 409);
        return request;
    }

    private static string SafeName(string value)
    {
        var cleaned = new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
        return cleaned.Length > 0 ? cleaned : "client";
    }

    // ---- Building responses -------------------------------------------------------------------------------------

    private async Task<MachineListResponse> BuildListAsync(InspectionRequest request, Guid actorId, string role, CancellationToken cancellationToken)
    {
        var machines = new List<MachineSummaryResponse>();
        foreach (var item in request.Items.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
        {
            var certificate = request.Certificates.SingleOrDefault(value => value.RequestItemId == item.Id);
            var (match, _) = await ResolveAsync(item, cancellationToken);
            var template = certificate is { UsesTemplate: true }
                ? CertificateTemplateCatalog.Find(certificate.TemplateId) ?? match.Template
                : match.Template;
            var answered = certificate is { UsesTemplate: true }
                ? certificate.Checklist.Keys.Count(key => template.ChecklistPaths.Contains(key))
                : 0;
            machines.Add(new MachineSummaryResponse(
                item.Id,
                item.Name,
                item.Quantity,
                item.SerialNumber,
                template.Key,
                StatusOf(certificate),
                template.ChecklistPaths.Count(),
                answered,
                certificate?.ReviewNote,
                certificate?.CertificateNumber,
                CanEditMachine(request, StatusOf(certificate), actorId, role),
                CanReviewMachine(request, StatusOf(certificate), role)));
        }

        var progress = new MachineProgress(
            machines.Count,
            machines.Count(value => value.Status is MachineStatus.Submitted or MachineStatus.Approved or MachineStatus.Delivered),
            machines.Count(value => value.Status is MachineStatus.Approved or MachineStatus.Delivered),
            machines.Count(value => value.Status == MachineStatus.Delivered));
        return new MachineListResponse(
            request.Id, request.Status, request.ClientName, request.Location, request.ScheduledAt, progress,
            CanStartInspection: request.Status is JobStatus.Assigned or JobStatus.TaskAccepted && CanEdit(request, actorId, role),
            CanDeliver: request.Status == JobStatus.CertificateApproved && CanDeliverJob(request, actorId, role),
            machines);
    }

    private async Task<MachineFormResponse> BuildFormAsync(
        InspectionRequest request,
        RequestItem item,
        InspectionCertificate? certificate,
        Guid actorId,
        string role,
        CancellationToken cancellationToken,
        Item? knownEquipment = null)
    {
        var (match, equipment) = knownEquipment is null
            ? await ResolveAsync(item, cancellationToken)
            : (CertificateTemplateCatalog.Resolve(item.Name, knownEquipment.Name), knownEquipment);
        var template = certificate is { UsesTemplate: true }
            ? CertificateTemplateCatalog.Find(certificate.TemplateId) ?? match.Template
            : match.Template;
        var client = await FindClientAsync(request, cancellationToken);
        var inspector = await userRepository.GetByIdAsync(certificate?.InspectorId ?? request.InspectorId ?? Guid.Empty, cancellationToken);

        var technical = certificate?.Technical ?? new Dictionary<string, string>();
        var checklist = certificate?.Checklist ?? new Dictionary<string, CertificateChecklistEntry>();
        var texts = certificate?.TextsOf() ?? new Dictionary<string, string>();
        var status = StatusOf(certificate);

        var fields = template.Fields
            .Select(field => new MachineFormField(field.Key, field.Label, technical.TryGetValue(field.Key, out var saved) ? saved : Prefill(field, item, equipment)))
            .ToArray();
        var sections = template.Checklist
            .Select(section => new MachineFormSection(
                section.Key,
                section.Title,
                section.Items.Select(row =>
                {
                    var path = $"{section.Key}.{row.Key}";
                    return checklist.TryGetValue(path, out var entry)
                        ? new MachineFormRow(path, row.Label, entry.Value, entry.Note)
                        : new MachineFormRow(path, row.Label, null, null);
                }).ToArray()))
            .ToArray();

        if (!texts.ContainsKey("clientRepresentative") && PrimaryContact(client) is { } representative)
            texts["clientRepresentative"] = representative;
        if (!texts.ContainsKey("testType") && !string.IsNullOrWhiteSpace(equipment?.TestType))
            texts["testType"] = equipment.TestType;

        var header = new MachineFormHeader(
            request.ClientName,
            client?.Location ?? request.Location,
            texts.GetValueOrDefault("clientRepresentative"),
            match.Item?.Name ?? item.Name,
            texts.TryGetValue("standardOfInspection", out var standardText)
                ? standardText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                : StandardsOf(match, equipment),
            equipment?.LastInspectionAt is { } lastInspection ? DateOnly.FromDateTime(lastInspection.UtcDateTime) : null,
            equipment?.NextInspectionDueAt is { } nextDue ? DateOnly.FromDateTime(nextDue.UtcDateTime) : null,
            inspector?.FullName);

        var editable = CanEditMachine(request, status, actorId, role);
        IReadOnlyList<string> missing = editable
            ? MachineFormRules.Problems(template, checklist, texts, certificate?.DatesOf() ?? new Dictionary<string, DateTimeOffset>(), certificate?.AnswersOf() ?? new Dictionary<string, bool>())
            : [];

        return new MachineFormResponse(
            request.Id,
            item.Id,
            request.Status,
            status,
            editable,
            CanReviewMachine(request, status, role),
            editable && missing.Count == 0,
            missing,
            template.Key,
            template.Name,
            header,
            fields,
            sections,
            ChecklistValues.All,
            certificate?.AnswersOf() ?? new Dictionary<string, bool>(),
            texts,
            ToDateOnly(certificate?.DatesOf() ?? new Dictionary<string, DateTimeOffset>()),
            certificate?.ReviewNote,
            certificate?.CertificateNumber);
    }

    private static CertificatePdfModel BuildPdfModel(
        InspectionRequest request,
        RequestItem item,
        InspectionCertificate certificate,
        CertificateTemplate template,
        TemplateMatch match,
        Item? equipment,
        Client? client,
        AppUser? inspector,
        IssuedCertificateSummary? previous)
    {
        var texts = MachineFormRules.WithDefaults(certificate.TextsOf());
        var dates = certificate.DatesOf();
        var answers = certificate.AnswersOf();
        var technical = certificate.Technical;
        var checklist = certificate.Checklist;

        DateOnly? Date(string key) => dates.TryGetValue(key, out var value) ? DateOnly.FromDateTime(value.UtcDateTime) : null;
        bool? Answer(string key) => answers.TryGetValue(key, out var value) ? value : null;
        string? Text(string key) => texts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

        var standards = Text("standardOfInspection") is { } saved
            ? saved.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : StandardsOf(match, equipment);
        var preparedName = Text("preparedByName") ?? inspector?.FullName;
        var qualifications = Text("preparedByQualifications") ??
                             (inspector is null ? null : string.Join(", ", inspector.Qualifications.Select(value => value.Name)));
        var preparedBy = string.Join(", ", new[] { preparedName, qualifications }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new CertificatePdfModel(
            certificate.CertificateNumber,
            certificate.IsApproved ? certificate.ReviewedAt : null,
            IsDraft: !certificate.IsApproved,
            request.ClientName,
            !string.IsNullOrWhiteSpace(client?.Location) ? client.Location : request.Location,
            Text("clientRepresentative") ?? PrimaryContact(client),
            previous?.ExaminationDate ??
                (equipment?.LastInspectionAt is { } last ? DateOnly.FromDateTime(last.UtcDateTime) : null),
            previous?.CertificateNumber,
            previous?.PreparedBy,
            Date("examination"),
            Date("nextExamination"),
            match.Item?.Name ?? item.Name,
            standards,
            Text("testType") ?? equipment?.TestType,
            Text("conclusion") ?? MachineFormRules.DefaultConclusion,
            template.Fields
                .Select(field => new CertificatePdfField(field.Label, technical.TryGetValue(field.Key, out var value) ? value : Prefill(field, item, equipment)))
                .ToArray(),
            new CertificatePdfQuestionnaire(
                Answer("firstExamination"),
                Answer("installedCorrectly"),
                Answer("within6Months"),
                Answer("within12Months"),
                Answer("examinationScheme"),
                Answer("exceptionalCircumstances"),
                Text("defectDescription") ?? MachineFormRules.DefaultDefects,
                Answer("existingDanger"),
                Answer("futureDanger"),
                Date("futureDangerBy"),
                Text("repairsRequired") ?? MachineFormRules.DefaultRepairs,
                Text("testsCarriedOut") ?? MachineFormRules.DefaultTests,
                Text("note")),
            template.Checklist
                .Select(section => new CertificatePdfSection(
                    section.Title,
                    section.Items.Select(row =>
                    {
                        var path = $"{section.Key}.{row.Key}";
                        return checklist.TryGetValue(path, out var entry)
                            ? new CertificatePdfRow(row.Label, entry.Value switch { ChecklistValues.Yes => "Yes", ChecklistValues.No => "No", _ => "N/A" }, entry.Note)
                            : new CertificatePdfRow(row.Label, null, null);
                    }).ToArray()))
                .ToArray(),
            preparedName,
            string.IsNullOrWhiteSpace(qualifications) ? null : qualifications,
            certificate.ReviewerName,
            certificate.SignatureStrokesJson,
            certificate.IsApproved ? certificate.QrData : null);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------

    private async Task<InspectionRequest> LoadAsync(
        Guid requestId, Guid actorId, string role, bool tracking, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        return request;
    }

    private static RequestItem FindItem(InspectionRequest request, Guid requestItemId) =>
        request.Items.SingleOrDefault(value => value.Id == requestItemId)
        ?? throw new KeyNotFoundException("Machine was not found.");

    private static bool CanEdit(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "technical_manager" || (role == "inspector" && request.InspectorId == actorId);

    private static void EnsureReviewer(string role)
    {
        if (role is not ("admin" or "technical_manager"))
            throw new AuthException("forbidden", "Only the technical manager can review certificates.", 403);
    }

    private static void EnsureUnderReview(InspectionRequest request)
    {
        if (request.Status is not (JobStatus.CertificateSubmitted or JobStatus.CertificateReturned))
            throw new AuthException("not_under_review", "Certificates can be reviewed once every machine has been submitted.", 409);
    }

    private static void EnsureTemplateCertificate(InspectionCertificate? certificate)
    {
        if (certificate is { UsesTemplate: false })
            throw new AuthException("legacy_certificate", "This certificate was created with the older form and cannot be opened here.", 409);
    }

    private async Task<(TemplateMatch Match, Item? Equipment)> ResolveAsync(RequestItem item, CancellationToken cancellationToken)
    {
        var equipment = item.EquipmentId is Guid id
            ? await equipmentRepository.GetByIdAsync(id, tracking: false, cancellationToken)
            : null;
        return (CertificateTemplateCatalog.Resolve(item.Name, equipment?.Name), equipment);
    }

    // The machine's previous certificate in the archive, printed as "Last Certificate No./Made by".
    private async Task<IssuedCertificateSummary?> PreviousCertificateAsync(InspectionCertificate certificate, CancellationToken cancellationToken) =>
        certificate.EquipmentId is Guid equipmentId
            ? await certificateFiles.FindLatestForEquipmentAsync(equipmentId, certificate.Id, cancellationToken)
            : null;

    private async Task<Client?> FindClientAsync(InspectionRequest request, CancellationToken cancellationToken) =>
        request.ClientId is Guid id ? await clientRepository.GetByIdAsync(id, tracking: false, cancellationToken) : null;

    private static string? PrimaryContact(Client? client) =>
        client?.Contacts.OrderByDescending(contact => contact.IsPrimary).FirstOrDefault()?.Name;

    // The standard the machine was registered with wins; otherwise the one the catalogue knows for that kind of machine.
    private static IReadOnlyList<string> StandardsOf(TemplateMatch match, Item? equipment) =>
        !string.IsNullOrWhiteSpace(equipment?.InspectionStandard)
            ? equipment.InspectionStandard.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : match.Item?.Standards ?? [];

    private static string? Prefill(TemplateField field, RequestItem item, Item? equipment) => field.Prefill switch
    {
        "name" => item.Name,
        "quantity" => item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "manufacturer" => item.Manufacturer ?? equipment?.Manufacturer,
        "capacity" => item.Capacity ?? equipment?.Capacity,
        "model" => string.Join(" / ", new[] { item.Model ?? equipment?.Model, equipment?.YearOfManufacture?.ToString() }.Where(value => !string.IsNullOrWhiteSpace(value))) is { Length: > 0 } model ? model : null,
        "serialNumber" => item.SerialNumber ?? equipment?.SerialNumber,
        _ => null
    };

    public static MachineStatus StatusOf(InspectionCertificate? certificate) => certificate switch
    {
        null => MachineStatus.NotStarted,
        { IsApproved: true, IsDelivered: true } => MachineStatus.Delivered,
        { IsApproved: true } => MachineStatus.Approved,
        { SubmittedAt: not null, ReviewedAt: null } => MachineStatus.Submitted,
        { ReviewedAt: not null } => MachineStatus.Returned,
        _ => MachineStatus.Draft
    };

    // Long enough that a code cannot be guessed from the QR of another certificate.
    private static string NewVerificationCode() => Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();

    private static Dictionary<string, T> Merge<T>(IReadOnlyDictionary<string, T> current, IReadOnlyDictionary<string, T> changes)
    {
        var result = new Dictionary<string, T>(current);
        foreach (var (key, value) in changes) result[key] = value;
        return result;
    }

    private static Dictionary<string, string> ReadFields(IReadOnlyDictionary<string, string>? fields, CertificateTemplate template)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in fields ?? new Dictionary<string, string>())
        {
            if (template.Fields.All(field => field.Key != key))
                throw new ArgumentException($"'{key}' is not a field of this certificate.");
            result[key] = Limit(value, key);
        }

        return result;
    }

    private static Dictionary<string, CertificateChecklistEntry> ReadChecklist(
        IReadOnlyDictionary<string, ChecklistAnswerRequest>? answers, CertificateTemplate template)
    {
        var paths = template.ChecklistPaths.ToHashSet();
        var result = new Dictionary<string, CertificateChecklistEntry>();
        foreach (var (key, answer) in answers ?? new Dictionary<string, ChecklistAnswerRequest>())
        {
            if (!paths.Contains(key)) throw new ArgumentException($"'{key}' is not a checklist row of this certificate.");
            var value = answer.Value?.Trim().ToLowerInvariant();
            if (!ChecklistValues.IsValid(value))
                throw new ArgumentException($"The answer for '{key}' must be yes, no or na.");
            result[key] = new CertificateChecklistEntry(value!, string.IsNullOrWhiteSpace(answer.Note) ? null : Limit(answer.Note.Trim(), key));
        }

        return result;
    }

    private static Dictionary<string, string> ReadTexts(IReadOnlyDictionary<string, string>? texts)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in texts ?? new Dictionary<string, string>())
        {
            if (!MachineFormRules.TextKeys.Contains(key))
                throw new ArgumentException($"'{key}' is not a text of this certificate.");
            result[key] = Limit(value, key);
        }

        return result;
    }

    private static Dictionary<string, DateTimeOffset> ReadDates(IReadOnlyDictionary<string, DateOnly>? dates)
    {
        var result = new Dictionary<string, DateTimeOffset>();
        foreach (var (key, value) in dates ?? new Dictionary<string, DateOnly>())
        {
            if (!MachineFormRules.DateKeys.Contains(key))
                throw new ArgumentException($"'{key}' is not a date of this certificate.");
            result[key] = new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return result;
    }

    private static Dictionary<string, DateOnly> ToDateOnly(IReadOnlyDictionary<string, DateTimeOffset> dates) =>
        dates.ToDictionary(pair => pair.Key, pair => DateOnly.FromDateTime(pair.Value.UtcDateTime));

    private static bool CanEditMachine(InspectionRequest request, MachineStatus status, Guid actorId, string role) =>
        status is MachineStatus.NotStarted or MachineStatus.Draft or MachineStatus.Returned &&
        request.Status is JobStatus.InProgress or JobStatus.CertificateReturned &&
        CanEdit(request, actorId, role);

    private static bool CanReviewMachine(InspectionRequest request, MachineStatus status, string role) =>
        role is "admin" or "technical_manager" && status == MachineStatus.Submitted &&
        request.Status is JobStatus.CertificateSubmitted or JobStatus.CertificateReturned;

    private static bool CanDeliverJob(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "technical_manager" || (role == "coordinator" && RequestAccess.CanManageQuotes(request, actorId, role));

    private static Dictionary<string, bool> ReadAnswers(IReadOnlyDictionary<string, bool>? answers)
    {
        var result = new Dictionary<string, bool>();
        foreach (var (key, value) in answers ?? new Dictionary<string, bool>())
        {
            if (!MachineFormRules.AnswerKeys.Contains(key))
                throw new ArgumentException($"'{key}' is not a question of this certificate.");
            result[key] = value;
        }

        return result;
    }

    private static string Limit(string value, string key) =>
        value.Length > MaxTextLength ? throw new ArgumentException($"'{key}' is too long.") : value.Trim();
}
