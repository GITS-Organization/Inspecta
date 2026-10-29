namespace Inspecta.Domain.Entities;

/// <summary>One priced line of a quotation, copied from a request item when the quotation is saved.</summary>
public sealed record QuoteItem(
    Guid? RequestItemId,
    string Type,
    int Quantity,
    long? UnitPricePiastres,
    string PricingUnit,
    string? Description = null,
    string? PrNumber = null)
{
    /// <summary>A line counts as priced once it has a price above zero.</summary>
    public bool IsPriced => UnitPricePiastres is > 0;

    public long TotalPiastres => checked(Quantity * (UnitPricePiastres ?? 0));

    public static QuoteItem Create(
        Guid? requestItemId,
        string type,
        int quantity,
        long? unitPricePiastres,
        string pricingUnit,
        string? description = null,
        string? prNumber = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Quote item type is required.", nameof(type));
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least one.");
        if (unitPricePiastres is < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPricePiastres));
        if (string.IsNullOrWhiteSpace(pricingUnit))
            throw new ArgumentException("Pricing unit is required.", nameof(pricingUnit));

        return new QuoteItem(
            requestItemId,
            type.Trim(),
            quantity,
            unitPricePiastres,
            pricingUnit.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            string.IsNullOrWhiteSpace(prNumber) ? null : prNumber.Trim());
    }
}
