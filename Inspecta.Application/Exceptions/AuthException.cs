namespace Inspecta.Application.Exceptions;

public sealed class AuthException(string code, string message, int statusCode = 401) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    /// <summary>What is wrong, one entry each, for the caller to show; sent as <c>problems</c> next to the message.</summary>
    public IReadOnlyList<string>? Problems { get; init; }
}