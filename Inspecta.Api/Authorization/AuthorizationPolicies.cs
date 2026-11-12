using Inspecta.Application.Authorization;

namespace Inspecta.Api.Authorization;

public static class AuthorizationPolicies
{
    /// <summary>The policy of a permission: one per entry of <see cref="Permissions"/>.</summary>
    public static string PolicyFor(string permission) => "perm:" + permission;

    public static IServiceCollection AddInspectaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // A controller or endpoint that forgets its access rule is closed, not open: public ones say [AllowAnonymous] on purpose.
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(PolicyFor(permission), policy =>
                {
                    policy.RequireRole(RolePermissions.RolesWith(permission));
                    // Everyone but the platform owner belongs to a company.
                    policy.RequireAssertion(context =>
                        context.User.IsInRole(RoleNames.Admin) ||
                        Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _));
                });
            }
        });

        return services;
    }
}
