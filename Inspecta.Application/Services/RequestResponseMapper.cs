using Inspecta.Application.Contracts.Requests;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

internal static class RequestResponseMapper
{
    public static RequestResponse Map(InspectionRequest request) => new(
        request.Id,
        request.ClientId,
        request.ClientName,
        request.Location,
        request.Subject,
        request.ReceivedAt,
        request.PreferredDate,
        request.Status,
        request.CoordinatorId,
        request.InspectorId,
        request.ScheduledAt,
        request.Source,
        request.EmailFrom,
        request.EmailPreview,
        request.Items.Select(MapItem).ToArray(),
        request.History.OrderBy(item => item.OccurredAt)
            .Select(item => new RequestHistoryResponse(item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Note)).ToArray(),
        request.ManagerId,
        request.EmailFromName,
        request.AccessNotes,
        request.GetMissingFields(),
        request.CanEditDetails,
        request.UpdatedAt,
        request.Triage,
        request.MaybeExpiresAt);

    public static RequestItemResponse MapItem(RequestItem item) => new(
        item.Id, item.EquipmentId, item.Name, item.Type, item.Capacity, item.Quantity,
        item.SerialNumber, item.IssueDescription, item.Location);

    public static RequestEmailResponse MapEmail(RequestEmail email) => new(
        email.RequestId,
        email.FromAddress,
        email.FromName,
        email.Subject,
        email.GetToAddresses().Select(MapAddress).ToArray(),
        email.GetCcAddresses().Select(MapAddress).ToArray(),
        email.GetBccAddresses().Select(MapAddress).ToArray(),
        email.GetReplyToAddresses().Select(MapAddress).ToArray(),
        email.MessageId,
        email.InReplyTo,
        email.References,
        email.SentAt,
        email.ReceivedAt,
        email.TextBody,
        email.HtmlBody,
        email.GetAttachments()
            .Select(attachment => new RequestEmailAttachmentResponse(attachment.FileName, attachment.ContentType, attachment.Size))
            .ToArray());

    private static RequestEmailAddressResponse MapAddress(EmailAddressInfo address) => new(address.Address, address.Name);
}
