using System.Reflection;
using System.Security.Claims;
using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Access is declared in one way: a permission on every endpoint, and a table that says which roles have which permission. These tests
/// keep it that way: no endpoint without a rule, no old-style policy or role list creeping back, every permission real and used, the
/// policies behave as the table says, and (until a later step changes a right on purpose) every endpoint allows exactly the roles it
/// allowed before the permission layer existed.
/// </summary>
public sealed class PermissionGuardTests
{
    /// <summary>
    /// The access of every action before the permission layer: "public", "any" (any signed-in user) or the roles allowed. Frozen from the
    /// attributes of the code at the start of the company and roles work. A later step that changes a right on purpose changes the line
    /// here in the same commit, so the change is visible in review.
    /// </summary>
    private static readonly Dictionary<string, string> AccessBefore = new()
    {
        ["AdminClientsController.List"] = "admin",
        ["AdminClientsController.Get"] = "admin",
        ["AdminClientsController.Create"] = "admin",
        ["AdminClientsController.Update"] = "admin",
        ["AdminClientsController.Delete"] = "admin",
        ["AdminCompanyProfilesController.Get"] = "admin",
        ["AdminCompanyProfilesController.Save"] = "admin",
        ["AuthController.Login"] = "public",
        ["AuthController.Refresh"] = "public",
        ["AuthController.GetCurrentUser"] = "any",
        ["AuthController.ChangeMyPassword"] = "any",
        ["AuthController.Logout"] = "any",
        ["BrandingController.TeamLogo"] = "public",
        ["BrandingController.Logo"] = "public",
        ["CertificatesController.List"] = "admin,technical_manager,coordinator,inspector",
        ["CertificatesController.Get"] = "admin,technical_manager,coordinator,inspector",
        ["CertificatesController.SaveDraft"] = "admin,technical_manager,inspector",
        ["CertificatesController.Submit"] = "admin,technical_manager,inspector",
        ["CertificatesController.Review"] = "admin,technical_manager",
        ["CertificationsController.Create"] = "admin,technical_manager,coordinator,inspector",
        ["CertificationsController.Clients"] = "admin,technical_manager,coordinator,inspector",
        ["CertificationsController.ClientMachines"] = "admin,technical_manager,coordinator,inspector",
        ["CertificationsController.MachineTypes"] = "admin,technical_manager,coordinator,inspector",
        ["CertificationsController.SerialLimits"] = "admin,technical_manager,coordinator,inspector",
        ["ClientCertificatesController.List"] = "admin,technical_manager,coordinator",
        ["ClientCertificatesController.DownloadPdf"] = "admin,technical_manager,coordinator",
        ["ClientCertificatesController.DownloadQrCode"] = "admin,technical_manager,coordinator",
        ["ClientEmailsController.List"] = "admin,technical_manager,coordinator",
        ["ClientEmailsController.Add"] = "admin,technical_manager,coordinator",
        ["ClientEmailsController.Remove"] = "admin,technical_manager,coordinator",
        ["ClientMailRulesController.List"] = "admin,technical_manager,coordinator",
        ["ClientMailRulesController.Create"] = "admin,technical_manager,coordinator",
        ["ClientMailRulesController.Delete"] = "admin,technical_manager,coordinator",
        ["ClientsController.List"] = "admin,technical_manager,coordinator",
        ["ClientsController.GetById"] = "admin,technical_manager,coordinator",
        ["ClientsController.GetHistory"] = "admin,technical_manager,coordinator",
        ["ClientsController.Create"] = "admin,technical_manager,coordinator",
        ["ClientsController.Update"] = "admin,technical_manager,coordinator",
        ["ClientsController.Archive"] = "admin,technical_manager",
        ["CoordinatorEmailConfigsController.Update"] = "admin,company_admin",
        ["CoordinatorEmailConfigsController.Delete"] = "admin,company_admin",
        ["CoordinatorEmailConfigsController.Get"] = "admin,company_admin",
        ["CoordinatorEmailConfigsController.ChangeSyncFrom"] = "admin,company_admin",
        ["DashboardController.Overview"] = "admin,technical_manager",
        ["EmailBacklogController.Cleanup"] = "admin",
        ["EmailsController.SendTest"] = "admin,technical_manager,coordinator",
        ["EquipmentController.List"] = "admin,technical_manager,coordinator",
        ["EquipmentController.GetById"] = "admin,technical_manager,coordinator",
        ["EquipmentController.Create"] = "admin,technical_manager,coordinator",
        ["EquipmentController.Update"] = "admin,technical_manager,coordinator",
        ["EquipmentController.RecordInspection"] = "admin,technical_manager,coordinator",
        ["EquipmentController.Archive"] = "admin,technical_manager",
        ["InspectorsController.List"] = "admin,technical_manager,coordinator",
        ["MachineInspectionController.StartInspection"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.ListMachines"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.OpenMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.SaveMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.AvailableMachines"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.AddMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.EditMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.DivideMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.RemoveMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.Split"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.UpdateCertificateDetails"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.SubmitAll"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.SubmitMachine"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.ApproveAll"] = "admin,technical_manager",
        ["MachineInspectionController.ReturnToInspector"] = "admin,technical_manager",
        ["MachineInspectionController.DownloadMachineQrCode"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.DownloadMachinePdf"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.PreviewCertificatesZip"] = "admin,technical_manager,coordinator,inspector",
        ["MachineInspectionController.PreviewDelivery"] = "admin,technical_manager,coordinator",
        ["MachineInspectionController.DownloadCertificatesZip"] = "admin,technical_manager,coordinator",
        ["MachineInspectionController.DeliverToClient"] = "admin,technical_manager,coordinator",
        ["MachineInspectionController.SendTestEmail"] = "admin,technical_manager,coordinator",
        ["MachineInspectionController.DeliverManually"] = "admin,technical_manager,coordinator",
        ["ManagerTeamController.List"] = "admin,company_admin",
        ["ManagerTeamController.Create"] = "admin,company_admin",
        ["ManagerTeamController.GetById"] = "admin,company_admin",
        ["ManagerTeamController.Update"] = "admin,company_admin",
        ["ManagerTeamController.SetStatus"] = "admin,company_admin",
        ["ManagerTeamController.SetPassword"] = "admin,company_admin",
        ["ManagerTeamController.SetEmail"] = "admin,company_admin",
        ["ManagerTeamController.Deactivate"] = "admin,company_admin",
        ["MicrosoftMailboxController.Status"] = "coordinator",
        ["MicrosoftMailboxController.Start"] = "coordinator",
        ["MicrosoftMailboxController.Callback"] = "public",
        ["PdfDiagnosticsController.Sample"] = "admin",
        ["QuotationDraftsController.List"] = "admin,technical_manager,coordinator",
        ["QuotationDraftsController.Update"] = "coordinator",
        ["QuotationDraftsController.Decide"] = "coordinator",
        ["QuotationQueueController.List"] = "admin,technical_manager,coordinator,inspector",
        ["QuotationQueueController.ListToReview"] = "admin,technical_manager",
        ["QuotationQueueController.ChangeInspectionDate"] = "admin,technical_manager,coordinator,inspector",
        ["QuotationsController.List"] = "admin,technical_manager,coordinator,inspector",
        ["QuotesController.List"] = "admin,technical_manager,coordinator",
        ["QuotesController.Get"] = "admin,technical_manager,coordinator",
        ["QuotesController.Create"] = "admin,technical_manager,coordinator",
        ["QuotesController.DismissReply"] = "admin,technical_manager,coordinator",
        ["QuotesController.Reopen"] = "admin,technical_manager,coordinator",
        ["QuotesController.Update"] = "admin,technical_manager,coordinator",
        ["QuotesController.Pdf"] = "admin,technical_manager,coordinator",
        ["QuotesController.Send"] = "admin,technical_manager,coordinator",
        ["QuotesController.PreviewEmail"] = "admin,technical_manager,coordinator",
        ["QuotesController.Accept"] = "admin,technical_manager,coordinator",
        ["QuotesController.Decision"] = "admin,technical_manager,coordinator",
        ["RequestsController.List"] = "admin,technical_manager,coordinator,inspector",
        ["RequestsController.Summary"] = "admin,technical_manager,coordinator,inspector",
        ["RequestsController.Confirm"] = "admin,technical_manager,coordinator",
        ["RequestsController.DeleteMaybe"] = "admin,technical_manager,coordinator",
        ["RequestsController.GetById"] = "admin,technical_manager,coordinator,inspector",
        ["RequestsController.Timeline"] = "admin,technical_manager,coordinator",
        ["RequestsController.Transfer"] = "admin,technical_manager,coordinator",
        ["RequestsController.ChangeReviewer"] = "admin,coordinator",
        ["CompanyTechnicalManagersController.List"] = "admin,technical_manager,coordinator,inspector,company_admin",
        ["AdminCompaniesController.List"] = "admin",
        ["AdminCompaniesController.Get"] = "admin",
        ["AdminCompaniesController.Create"] = "admin",
        ["AdminCompaniesController.Rename"] = "admin",
        ["AdminCompaniesController.SetStatus"] = "admin",
        ["AdminCompaniesController.UpdateSubscription"] = "admin",
        // Invoices and payments (2026-11-13): new in this step, so the lines are the agreed rights, not a record of the past.
        ["InvoicesController.List"] = "admin,coordinator,financial_manager,general_manager",
        ["InvoicesController.Get"] = "admin,coordinator,financial_manager,general_manager",
        ["InvoicesController.CreateForRequest"] = "admin,coordinator",
        ["InvoicesController.RecordPayment"] = "admin,coordinator,financial_manager",
        ["InvoicesController.ReversePayment"] = "admin,coordinator,financial_manager",
        ["InvoicesController.HandOver"] = "admin,coordinator",
        ["InvoicesController.Close"] = "admin,financial_manager",
        ["InvoicesController.Revenue"] = "admin,financial_manager,general_manager",
        ["CompanyController.Get"] = "admin,technical_manager,coordinator,inspector,company_admin",
        ["CompanyController.SetDefaultReviewer"] = "admin,company_admin",
        ["CompanyController.GetProfile"] = "admin,company_admin",
        ["CompanyController.SaveProfile"] = "admin,company_admin",
        ["RequestsController.Create"] = "admin,technical_manager,coordinator",
        ["RequestsController.AssignWithoutQuotation"] = "admin,technical_manager,coordinator",
        ["RequestsController.CreateManDay"] = "admin,technical_manager,coordinator",
        ["RequestsController.Suggestions"] = "admin,technical_manager,coordinator,inspector",
        ["RequestsController.Autofill"] = "admin,technical_manager,coordinator",
        ["RequestsController.GetEmail"] = "admin,technical_manager,coordinator,inspector",
        ["RequestsController.UpdateDetails"] = "admin,technical_manager,coordinator",
        ["RequestsController.AddItem"] = "admin,technical_manager,coordinator",
        ["RequestsController.UpdateItem"] = "admin,technical_manager,coordinator",
        ["RequestsController.RemoveItem"] = "admin,technical_manager,coordinator",
        ["RequestsController.Reject"] = "admin,technical_manager,coordinator",
        ["RequestsController.Transition"] = "admin,technical_manager,coordinator,inspector",
        ["RequestsController.Assign"] = "admin,technical_manager,coordinator",
        ["TeamCoordinatorsController.List"] = "admin,technical_manager,coordinator",
        ["TechnicalManagersController.Create"] = "admin",
        ["TechnicalManagersController.UpdateSubscription"] = "admin",
        ["UsersController.List"] = "admin",
        ["UsersController.GetById"] = "admin",
        ["UsersController.Create"] = "admin",
        ["UsersController.UpdateProfile"] = "admin",
        ["UsersController.SetStatus"] = "admin",
        ["UsersController.SetPassword"] = "admin",
        ["UsersController.SetEmail"] = "admin",
        ["UsersController.Deactivate"] = "admin",
        ["VerificationController.Page"] = "public",
        ["VerificationController.Pdf"] = "public",
        ["VerificationController.Json"] = "public",
    };

    private static IEnumerable<(string Key, MethodInfo Action, Type Controller)> Actions() =>
        typeof(RequirePermissionAttribute).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes().Any(attribute => attribute is HttpMethodAttribute))
                .Select(method => ($"{type.Name}.{method.Name}", method, type)));

    private static string AccessOf(MethodInfo action) =>
        action.GetCustomAttribute<RequirePermissionAttribute>() is { } required
            ? string.Join(",", RolePermissions.RolesWith(required.Permission))
            : action.GetCustomAttribute<AllowAnonymousAttribute>() is not null || action.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                ? "public"
                : "any";

    [Fact]
    public void EveryActionDeclaresExactlyOneAccessRule()
    {
        var problems = new List<string>();
        foreach (var (key, action, controller) in Actions())
        {
            var permission = action.GetCustomAttributes<RequirePermissionAttribute>().Count();
            var anonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null || controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null ? 1 : 0;
            var signedIn = action.GetCustomAttributes<AuthorizeAttribute>().Count(attribute => attribute is not RequirePermissionAttribute);
            if (permission + anonymous + signedIn != 1)
                problems.Add($"{key}: {permission} permission, {anonymous} anonymous, {signedIn} plain [Authorize] (exactly one is needed)");
            foreach (var plain in action.GetCustomAttributes<AuthorizeAttribute>().Where(attribute => attribute is not RequirePermissionAttribute))
                if (plain.Policy is not null || plain.Roles is not null)
                    problems.Add($"{key}: a policy or a role list on [Authorize]; use [RequirePermission] and the role map instead");
        }

        foreach (var controller in Actions().Select(item => item.Controller).Distinct())
            if (controller.GetCustomAttributes<AuthorizeAttribute>().Any())
                problems.Add($"{controller.Name}: [Authorize] on the class; declare the rule on each action");

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void EveryPermissionUsedExistsAndEveryPermissionIsGrantedAndUsed()
    {
        var used = Actions().Select(item => item.Action.GetCustomAttribute<RequirePermissionAttribute>()?.Permission).Where(value => value is not null).ToHashSet();

        Assert.All(used, permission => Assert.Contains(permission, Permissions.All));
        Assert.All(Permissions.All, permission => Assert.NotEmpty(RolePermissions.RolesWith(permission)));
        Assert.Empty(Permissions.All.Where(permission => !used.Contains(permission)));
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
    }

    [Fact]
    public void EveryEndpointAllowsExactlyTheRolesItAllowedBeforeThePermissionLayer()
    {
        var now = Actions().ToDictionary(item => item.Key, item => AccessOf(item.Action));

        var differences = AccessBefore.Keys.Union(now.Keys).OrderBy(key => key)
            .Where(key => !AccessBefore.TryGetValue(key, out var before) || !now.TryGetValue(key, out var after) || before != after)
            .Select(key => $"{key}: before '{(AccessBefore.TryGetValue(key, out var b) ? b : "(none)")}', now '{(now.TryGetValue(key, out var a) ? a : "(none)")}'");

        Assert.True(!differences.Any(), string.Join(Environment.NewLine, differences));
    }

    private static ClaimsPrincipal User(string role, bool withCompany) =>
        new(new ClaimsIdentity(
            withCompany ? [new Claim("role", role), new Claim("tenant_id", Guid.NewGuid().ToString())] : [new Claim("role", role)],
            "test", "name", "role"));

    [Fact]
    public async Task ThePoliciesFollowTheRoleMapAndEveryoneButThePlatformOwnerNeedsACompany()
    {
        var services = new ServiceCollection().AddLogging().AddInspectaAuthorization().BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();

        foreach (var permission in Permissions.All)
        foreach (var role in RoleNames.All)
        {
            var policy = AuthorizationPolicies.PolicyFor(permission);
            var withCompany = (await authorization.AuthorizeAsync(User(role, withCompany: true), null, policy)).Succeeded;
            var without = (await authorization.AuthorizeAsync(User(role, withCompany: false), null, policy)).Succeeded;

            Assert.True(withCompany == RolePermissions.Has(role, permission), $"{role} / {permission}: policy {withCompany}, map {RolePermissions.Has(role, permission)}");
            Assert.True(without == (role == RoleNames.Admin && RolePermissions.Has(role, permission)), $"{role} / {permission} without a company: {without}");
        }
    }

    [Fact]
    public void ARoleStringThatIsNotKnownHasNoPermission()
    {
        Assert.Empty(RolePermissions.For("company_admin_typo"));
        Assert.Empty(RolePermissions.For(null));
        Assert.False(RolePermissions.Has("", Permissions.RequestsView));
        Assert.Equal(RoleNames.All.Count, RoleNames.All.Distinct().Count());
    }
}
