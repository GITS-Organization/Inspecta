using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Domain.Tests;

/// <summary>The layers only look inward: Api and Infrastructure on Application, Application on Domain, Domain on nothing of ours.</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Inspecta.Domain.Entities.AppUser).Assembly;
    private static readonly Assembly Application = typeof(Inspecta.Application.Services.AuthService).Assembly;
    private static readonly Assembly Infrastructure = typeof(Inspecta.Infrastructure.Data.InspectaDbContext).Assembly;
    private static readonly Assembly Api = typeof(Inspecta.Api.Controllers.AuthController).Assembly;

    private static string[] ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();

    [Fact]
    public void TheDomainDependsOnNoneOfOurOtherProjectsAndNoFramework()
    {
        var references = ReferencesOf(Domain);

        Assert.DoesNotContain(references, name => name.StartsWith("Inspecta.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void TheApplicationKnowsTheDomainButNeitherTheDatabaseNorTheWebFramework()
    {
        var references = ReferencesOf(Application);

        Assert.Contains("Inspecta.Domain", references);
        Assert.DoesNotContain("Inspecta.Infrastructure", references);
        Assert.DoesNotContain("Inspecta.Api", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void TheInfrastructureDoesNotKnowTheApi()
    {
        Assert.DoesNotContain("Inspecta.Api", ReferencesOf(Infrastructure));
    }

    [Fact]
    public void ControllersTalkToServicesNeverToRepositoriesOrTheDatabase()
    {
        var offenders = Api.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)) && !type.IsAbstract)
            .SelectMany(controller => controller.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Assembly == Infrastructure ||
                                    parameter.ParameterType.Name.EndsWith("Repository", StringComparison.Ordinal))
                .Select(parameter => $"{controller.Name} takes {parameter.ParameterType.Name}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ApplicationServicesNeverTakeInfrastructureTypes()
    {
        var offenders = Application.GetTypes()
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Assembly == Infrastructure)
                .Select(parameter => $"{type.Name} takes {parameter.ParameterType.Name}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void DomainEntitiesCannotBeChangedFromOutsideByPublicSetters()
    {
        var offenders = Domain.GetTypes()
            .Where(type => type.Namespace == "Inspecta.Domain.Entities" && type.IsClass && !type.IsAbstract && !type.IsNested)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true } && property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().All(m => m.Name != "IsExternalInit"))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToArray();

        Assert.Empty(offenders);
    }
}
