using Microsoft.AspNetCore.Authorization;

namespace Inspecta.Api.Authorization;

/// <summary>
/// The access rule of an endpoint: the caller's role must have this permission (see <c>RolePermissions</c>) and, except for the platform
/// owner, the token must carry a company. Every action declares exactly one of this, <c>[AllowAnonymous]</c> or a plain
/// <c>[Authorize]</c> (any signed-in user); a test fails the build otherwise.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) : base(AuthorizationPolicies.PolicyFor(permission)) =>
        Permission = permission;

    public string Permission { get; }
}
