using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class QuotationDraftService(IQuotationDraftRepository draftRepository) : IQuotationDraftService
{
    public async Task<IReadOnlyList<QuotationDraftResponse>> ListAsync(
        Guid? tenantId,
        Guid? coordinatorId,
        CancellationToken cancellationToken = default) =>
        (await draftRepository.ListAsync(tenantId, coordinatorId, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<QuotationDraftResponse?> UpdateAsync(
        Guid coordinatorId,
        Guid draftId,
        UpdateQuotationDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var draft = await draftRepository.GetForCoordinatorAsync(
            coordinatorId,
            draftId,
            tracking: true,
            cancellationToken);
        if (draft is null)
            return null;

        draft.UpdateDetails(
            request.ClientName,
            request.Location,
            request.MachineDetails,
            request.AmountPiastres,
            request.ValidUntil);
        await draftRepository.SaveAsync(cancellationToken);
        return Map(draft);
    }

    public async Task<QuotationDraftResponse?> DecideAsync(
        Guid coordinatorId,
        Guid draftId,
        QuotationDraftDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var draft = await draftRepository.GetForCoordinatorAsync(
            coordinatorId,
            draftId,
            tracking: true,
            cancellationToken);
        if (draft is null)
            return null;

        draft.SetDecision(request.Status);
        await draftRepository.SaveAsync(cancellationToken);
        return Map(draft);
    }

    private static QuotationDraftResponse Map(QuotationDraft draft) => new(
        draft.Id,
        draft.CoordinatorId,
        draft.CoordinatorName,
        draft.ClientName,
        draft.Location,
        draft.MachineDetails,
        draft.AmountPiastres,
        draft.ValidUntil,
        draft.RawEmailBody,
        draft.EmailHtmlBody,
        draft.SenderEmail,
        draft.SenderName,
        draft.EmailSubject,
        draft.GetToAddresses().Select(address =>
            new QuotationEmailAddressResponse(address.Address, address.Name)).ToArray(),
        draft.GetCcAddresses().Select(address =>
            new QuotationEmailAddressResponse(address.Address, address.Name)).ToArray(),
        draft.GetBccAddresses().Select(address =>
            new QuotationEmailAddressResponse(address.Address, address.Name)).ToArray(),
        draft.GetReplyToAddresses().Select(address =>
            new QuotationEmailAddressResponse(address.Address, address.Name)).ToArray(),
        draft.GetAttachments().Select(attachment =>
            new QuotationEmailAttachmentResponse(attachment.FileName, attachment.ContentType, attachment.Size)).ToArray(),
        draft.EmailMessageId,
        draft.InReplyTo,
        draft.References,
        draft.EmailDate,
        draft.Status,
        draft.CreatedAt);
}
