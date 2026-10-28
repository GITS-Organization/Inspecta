namespace Inspecta.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string TenantManagement = "TenantManagement";
    public const string TeamOperations = "TeamOperations";
    public const string TeamAccess = "TeamAccess";

    public static IServiceCollection AddInspectaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminOnly, policy =>
                policy.RequireRole("admin"));
            options.AddPolicy(TenantManagement, policy =>
                policy.RequireRole("admin", "technical_manager"));
            options.AddPolicy(TeamOperations, policy =>
            {
                policy.RequireRole("admin", "technical_manager", "coordinator");
                policy.RequireAssertion(context =>
                    context.User.IsInRole("admin") ||
                    Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _));
            });
            options.AddPolicy(TeamAccess, policy =>
            {
                policy.RequireRole("admin", "technical_manager", "coordinator", "inspector");
                policy.RequireAssertion(context =>
                    context.User.IsInRole("admin") ||
                    Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _));
            });
        });

        return services;
    }
}
