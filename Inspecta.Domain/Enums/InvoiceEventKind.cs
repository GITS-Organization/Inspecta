namespace Inspecta.Domain.Enums;

/// <summary>The steps of an invoice kept in its history besides the payments, which are their own rows.</summary>
public enum InvoiceEventKind
{
    Created,
    Emailed,
    HandedOver,
    Closed
}
