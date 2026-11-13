namespace Inspecta.Domain.Exceptions;

/// <summary>An invoice rule was broken. <see cref="Code"/> is the short name the API answers with (for example <c>overpayment</c>).</summary>
public sealed class InvoiceRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>True for the codes that mean the caller sent something wrong (answered 400); the others are a state conflict (409).</summary>
    public static bool IsBadInput(string code) =>
        code is "payment_date_invalid" or "payment_amount_invalid" or "payment_label_too_long" or "reason_required" or "reason_too_long";
}
