using Inspecta.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Api.Middleware;

public sealed class AuthExceptionHandler(ILogger<AuthExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, code, detail) = exception switch
        {
            AuthException auth => (auth.StatusCode, "Request rejected", auth.Code, auth.Message),
            EmailEncryptionConfigurationException => (
                StatusCodes.Status503ServiceUnavailable,
                "Email encryption is unavailable",
                "email_encryption_not_configured",
                "Coordinator email cannot be saved because IMAP_ENCRYPTION_KEY is missing or invalid. Configure a Base64-encoded 32-byte key in the deployment environment."),
            EmailDeliveryException => (StatusCodes.Status502BadGateway, "Email could not be sent", "email_delivery_failed", exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found", "not_found", "The requested resource was not found."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", "bad_request", exception.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflict", "data_conflict", "The data conflicts with an existing record."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict", "conflict", "The requested action conflicts with the current state."),
            _ => (0, string.Empty, string.Empty, string.Empty)
        };
        if (statusCode == 0) return false;

        logger.LogInformation("Request rejected with {Code}.", code);
        httpContext.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}