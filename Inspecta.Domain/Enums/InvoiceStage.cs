namespace Inspecta.Domain.Enums;

/// <summary>Where an invoice is: made and followed by the coordinator, with the financial manager, or closed.</summary>
public enum InvoiceStage
{
    Open,
    WithFinance,
    Closed
}
