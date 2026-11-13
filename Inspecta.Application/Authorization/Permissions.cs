namespace Inspecta.Application.Authorization;

/// <summary>
/// Everything a signed-in user can be allowed to do, named by what it is for. Endpoints ask for one of these
/// (<c>[RequirePermission(Permissions.X)]</c>); <see cref="RolePermissions"/> says which roles have which. To change what a role may do,
/// change the map there, never an endpoint.
/// </summary>
public static class Permissions
{
    // The platform owner (Gits) and the companies.
    public const string PlatformAdminister = "Platform.Administer";
    public const string CompanyEditIdentity = "Company.EditIdentity";
    public const string AccountsManage = "Accounts.Manage";
    public const string CompanyView = "Company.View";
    public const string CompanySetDefaultReviewer = "Company.SetDefaultReviewer";

    public const string DashboardView = "Dashboard.View";

    // Reviewing certificates (approving, returning, the review queue).
    public const string ReviewAct = "Review.Act";
    public const string ReviewViewQueue = "Review.ViewQueue";

    public const string ClientsView = "Clients.View";
    public const string ClientsEdit = "Clients.Edit";
    public const string ClientsDelete = "Clients.Delete";

    public const string EquipmentView = "Equipment.View";
    public const string EquipmentEdit = "Equipment.Edit";
    public const string EquipmentDelete = "Equipment.Delete";

    public const string CertificatesView = "Certificates.View";
    public const string CertificatesViewIssued = "Certificates.ViewIssued";
    public const string CertificatesCreateDirect = "Certificates.CreateDirect";
    public const string CertificatesDeliver = "Certificates.Deliver";

    public const string InspectionWork = "Inspection.Work";
    public const string InspectionWriteLegacy = "Inspection.WriteLegacy";
    public const string InspectionChangeDate = "Inspection.ChangeDate";

    public const string RequestsView = "Requests.View";
    public const string RequestsWork = "Requests.Work";
    public const string RequestsAssign = "Requests.Assign";
    public const string RequestsTransition = "Requests.Transition";
    public const string RequestsTimeline = "Requests.Timeline";
    public const string RequestsTransfer = "Requests.Transfer";
    public const string RequestsChangeReviewer = "Requests.ChangeReviewer";

    public const string QuotesView = "Quotes.View";
    public const string QuotesListAll = "Quotes.ListAll";
    public const string QuotesWork = "Quotes.Work";

    // Invoices: what a client owes for an accepted quotation, and the money received on it.
    public const string InvoicesView = "Invoices.View";
    public const string InvoicesCreate = "Invoices.Create";
    public const string InvoicesRecordPayment = "Invoices.RecordPayment";
    public const string InvoicesHandOver = "Invoices.HandOver";
    public const string InvoicesEmailClient = "Invoices.EmailClient";
    public const string InvoicesClose = "Invoices.Close";
    public const string ExecutiveView = "Executive.View";
    public const string ExecutiveManageTargets = "Executive.ManageTargets";
    public const string RevenueView = "Revenue.View";

    public const string DraftsView = "Drafts.View";
    public const string DraftsWork = "Drafts.Work";

    public const string TeamDirectory = "Team.Directory";
    public const string TeamReviewers = "Team.Reviewers";

    public const string MailboxTest = "Mailbox.Test";
    public const string MailboxConnectOwn = "Mailbox.ConnectOwn";

    /// <summary>Every permission, for registering policies and for the tests that check the map is complete.</summary>
    public static readonly IReadOnlyList<string> All = typeof(Permissions)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
