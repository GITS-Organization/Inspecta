namespace Inspecta.Domain.Entities;

public sealed record QuoteItem(
    Guid? RequestItemId,
    string Type,
    int Quantity,
    long UnitPricePiastres,
    string PricingUnit)
{
    public long TotalPiastres => checked(Quantity * UnitPricePiastres);

    public static QuoteItem Create(
        Guid? requestItemId,
        string type,
        int quantity,
        long unitPricePiastres,
        string pricingUnit)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Quote item type is required.", nameof(type));
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least one.");
        if (unitPricePiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPricePiastres));
        if (string.IsNullOrWhiteSpace(pricingUnit))
            throw new ArgumentException("Pricing unit is required.", nameof(pricingUnit));

        return new QuoteItem(requestItemId, type.Trim(), quantity, unitPricePiastres, pricingUnit.Trim());
    }
}
