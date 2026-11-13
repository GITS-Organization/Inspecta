namespace Inspecta.Domain.Exceptions;

/// <summary>An invoice rule was broken. <see cref="Code"/> is the short name the API answers with (for example <c>overpayment</c>).</summary>
public sealed class InvoiceRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
