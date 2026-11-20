using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>Who may see and work on a request. Team scope itself comes from the data layer tenant filter.</summary>
internal static class RequestAccess
{
    // Inspectors only see their assigned jobs. A coordinator sees only the requests they own, manual or email (an email request belongs to the
    // coordinator whose mailbox received it; a request is handed to a colleague with the transfer). Managers and Admins see everything the tenant filter allows.
    public static bool IsVisible(InspectionRequest request, Guid actorId, string role) => role switch
    {
        "inspector" => request.InspectorId == actorId,
        "coordinator" => request.CoordinatorId == actorId,
        _ => true
    };

    // A coordinator manages the quotes of the requests they own; managers can manage those of manual requests but only read the quotes of email requests; the admin can do everything.
    public static bool CanManageQuotes(InspectionRequest request, Guid actorId, string role) =>
        role == "admin" ||
        IsVisible(request, actorId, role) && (request.Source != RequestSource.Email || request.CoordinatorId == actorId);
}
