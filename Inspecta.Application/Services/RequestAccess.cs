using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>Who may see and work on a request. Team scope itself comes from the data layer tenant filter.</summary>
internal static class RequestAccess
{
    // Inspectors only see their assigned jobs. Coordinators see manual requests of their team but only the
    // email requests that arrived in their own mailbox. Managers and Admins see everything the tenant filter allows.
    public static bool IsVisible(InspectionRequest request, Guid actorId, string role) => role switch
    {
        "inspector" => request.InspectorId == actorId,
        "coordinator" => request.Source != RequestSource.Email || request.CoordinatorId == actorId,
        _ => true
    };

    // Email requests belong to the coordinator that received them; managers and admins can read their quotes only.
    public static bool CanManageQuotes(InspectionRequest request, Guid actorId, string role) =>
        IsVisible(request, actorId, role) && (request.Source != RequestSource.Email || request.CoordinatorId == actorId);
}
