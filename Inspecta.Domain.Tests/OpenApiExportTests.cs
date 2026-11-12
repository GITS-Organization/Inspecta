namespace Inspecta.Domain.Tests;

/// <summary>
/// Writes the API contract (OpenAPI, as the running API describes itself) to the file named by the environment variable
/// <c>EXPORT_OPENAPI_TO</c>, for the frontend hand-off. Does nothing when the variable is not set, so it is a no-op in a normal run.
/// </summary>
public sealed class OpenApiExportTests(ApiHttpTests.Api api) : IClassFixture<ApiHttpTests.Api>
{
    [Fact]
    public async Task ExportsTheContractWhenAskedTo()
    {
        var target = Environment.GetEnvironmentVariable("EXPORT_OPENAPI_TO");
        if (string.IsNullOrWhiteSpace(target)) return;

        api.EnsureSeeded();
        var json = await api.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        await File.WriteAllTextAsync(target, json);
        Assert.Contains("\"/api/v1/company/technical-managers\"", json);
    }
}

/// <summary>Writes who may do what (the role map) to the file named by <c>EXPORT_PERMISSIONS_TO</c>, for the frontend hand-off. A no-op otherwise.</summary>
public sealed class PermissionExportTests
{
    [Fact]
    public async Task ExportsTheRoleMapWhenAskedTo()
    {
        var target = Environment.GetEnvironmentVariable("EXPORT_PERMISSIONS_TO");
        if (string.IsNullOrWhiteSpace(target)) return;

        var map = Inspecta.Application.Authorization.RoleNames.All
            .ToDictionary(role => role, role => Inspecta.Application.Authorization.RolePermissions.For(role));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        await File.WriteAllTextAsync(target, System.Text.Json.JsonSerializer.Serialize(map, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Assert.NotEmpty(map["coordinator"]);
    }
}
