namespace Inspecta.Application.Exceptions;

public sealed class AuthException(string code, string message, int statusCode = 401) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}