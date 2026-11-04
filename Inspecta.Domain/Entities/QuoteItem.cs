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
    /// <summary>The type of the line that sells an inspector's day: it is not a machine, so it has no request item.</summary>
    public const string ManDayType = "Man day";

    public const string ManDayPricingUnit = "Per Day";

    /// <summary>True for the man day line of a quotation.</summary>
    public bool IsManDay => RequestItemId is null && Type == ManDayType;

    /// <summary>The man day line: <paramref name="days"/> days of one inspector at a price per day.</summary>
    public static QuoteItem ManDay(int days, long? pricePerDayPiastres) =>
        Create(null, ManDayType, days, pricePerDayPiastres, ManDayPricingUnit, "Man day (inspector)");

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
