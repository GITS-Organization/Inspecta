namespace Inspecta.Application.Authorization;

/// <summary>The role strings carried in the token (<c>role</c> claim).</summary>
public static class RoleNames
{
    /// <summary>The platform owner. Not a company role: it sees every company.</summary>
    public const string Admin = "admin";
    public const string TechnicalManager = "technical_manager";
    public const string Coordinator = "coordinator";
    public const string Inspector = "inspector";

    public static readonly IReadOnlyList<string> All = [Admin, TechnicalManager, Coordinator, Inspector];
}

/// <summary>
/// The one place that says which role may do what. Each line gives a permission to the roles that have it. Everything else in the
/// system (policies, endpoint attributes, the dashboard's <c>permissions</c> list) is derived from this table, so changing a role's rights
/// is a change to one line and its test.
/// </summary>
public static class RolePermissions
{
    private const string Admin = RoleNames.Admin;
    private const string Manager = RoleNames.TechnicalManager;
    private const string Coordinator = RoleNames.Coordinator;
    private const string Inspector = RoleNames.Inspector;

    private static readonly (string Permission, string[] Roles)[] Grants =
    [
        (Permissions.PlatformAdminister, [Admin]),
        (Permissions.CompanyEditIdentity, [Admin]),
        (Permissions.AccountsManage, [Admin, Manager]),

        (Permissions.DashboardView, [Admin, Manager]),

        (Permissions.ReviewAct, [Admin, Manager]),
        (Permissions.ReviewViewQueue, [Admin, Manager]),

        (Permissions.ClientsView, [Admin, Manager, Coordinator]),
        (Permissions.ClientsEdit, [Admin, Manager, Coordinator]),
        (Permissions.ClientsDelete, [Admin, Manager]),

        (Permissions.EquipmentView, [Admin, Manager, Coordinator]),
        (Permissions.EquipmentEdit, [Admin, Manager, Coordinator]),
        (Permissions.EquipmentDelete, [Admin, Manager]),

        (Permissions.CertificatesView, [Admin, Manager, Coordinator, Inspector]),
        (Permissions.CertificatesViewIssued, [Admin, Manager, Coordinator]),
        (Permissions.CertificatesCreateDirect, [Admin, Manager, Coordinator, Inspector]),
        (Permissions.CertificatesDeliver, [Admin, Manager, Coordinator]),

        (Permissions.InspectionWork, [Admin, Manager, Coordinator, Inspector]),
        (Permissions.InspectionWriteLegacy, [Admin, Manager, Inspector]),
        (Permissions.InspectionChangeDate, [Admin, Manager, Coordinator, Inspector]),

        (Permissions.RequestsView, [Admin, Manager, Coordinator, Inspector]),
        (Permissions.RequestsWork, [Admin, Manager, Coordinator]),
        (Permissions.RequestsAssign, [Admin, Manager, Coordinator]),
        (Permissions.RequestsTransition, [Admin, Manager, Coordinator, Inspector]),
        (Permissions.RequestsTimeline, [Admin, Manager, Coordinator]),
        (Permissions.RequestsTransfer, [Admin, Manager, Coordinator]),
        (Permissions.RequestsChangeReviewer, [Admin, Coordinator]),

        (Permissions.QuotesView, [Admin, Manager, Coordinator]),
        (Permissions.QuotesListAll, [Admin, Manager, Coordinator, Inspector]),
        (Permissions.QuotesWork, [Admin, Manager, Coordinator]),

        (Permissions.DraftsView, [Admin, Manager, Coordinator]),
        (Permissions.DraftsWork, [Coordinator]),

        (Permissions.TeamDirectory, [Admin, Manager, Coordinator]),
        (Permissions.TeamReviewers, [Admin, Manager, Coordinator, Inspector]),

        (Permissions.MailboxTest, [Admin, Manager, Coordinator]),
        (Permissions.MailboxConnectOwn, [Coordinator])
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolesByPermission =
        Grants.ToDictionary(grant => grant.Permission, grant => (IReadOnlyList<string>)grant.Roles);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PermissionsByRole =
        RoleNames.All.ToDictionary(
            role => role,
            role => (IReadOnlyList<string>)Grants.Where(grant => grant.Roles.Contains(role)).Select(grant => grant.Permission).ToArray());

    /// <summary>The roles that have the permission (empty for a permission nobody was given).</summary>
    public static IReadOnlyList<string> RolesWith(string permission) =>
        RolesByPermission.TryGetValue(permission, out var roles) ? roles : [];

    /// <summary>The permissions of a role (empty for an unknown role).</summary>
    public static IReadOnlyList<string> For(string? role) =>
        role is not null && PermissionsByRole.TryGetValue(role, out var permissions) ? permissions : [];

    public static bool Has(string? role, string permission) => RolesWith(permission).Contains(role ?? string.Empty);
}
