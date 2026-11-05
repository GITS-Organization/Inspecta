using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Inspecta.Domain.Tests;

/// <summary>Every endpoint of the API says who may call it; the public ones are listed here, so a new one cannot be public by accident.</summary>
public sealed class ApiAuthorizationGuardTests
{
    private static readonly Type[] Controllers = typeof(Inspecta.Api.Controllers.AuthController).Assembly.GetTypes()
        .Where(type => type.IsSubclassOf(typeof(ControllerBase)) && !type.IsAbstract)
        .ToArray();

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions() =>
        Controllers.SelectMany(controller => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .Select(method => (controller, method)));

    private static bool Has<T>(Type controller, MethodInfo action) where T : Attribute =>
        action.GetCustomAttributes<T>(inherit: true).Any() || controller.GetCustomAttributes<T>(inherit: true).Any();

    [Fact]
    public void EveryActionIsEitherAuthorizedOrDeliberatelyAnonymous()
    {
        var undecided = Actions()
            .Where(entry => !Has<AuthorizeAttribute>(entry.Controller, entry.Action) && !Has<AllowAnonymousAttribute>(entry.Controller, entry.Action))
            .Select(entry => $"{entry.Controller.Name}.{entry.Action.Name}")
            .ToArray();

        Assert.Empty(undecided);
    }

    [Fact]
    public void OnlyTheKnownPublicActionsAreAnonymous()
    {
        var anonymous = Actions()
            .Where(entry => Has<AllowAnonymousAttribute>(entry.Controller, entry.Action))
            .Select(entry => $"{entry.Controller.Name}.{entry.Action.Name}")
            .Order()
            .ToArray();

        // Sign-in and token refresh, the branding logos, and the public certificate check behind the QR code.
        Assert.Equal(
            new[]
            {
                "AuthController.Login", "AuthController.Refresh",
                "BrandingController.Logo", "BrandingController.TeamLogo",
                "VerificationController.Json", "VerificationController.Pdf", "VerificationController.Page"
            }.Order().ToArray(),
            anonymous);
    }
}
