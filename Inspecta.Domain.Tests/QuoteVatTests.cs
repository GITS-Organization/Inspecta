using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

/// <summary>The optional VAT of a quotation: a percentage (14 by default, 0 for none) worked out on the total after the discount.</summary>
public sealed class QuoteVatTests
{
    private static Quote QuoteOf(params (long UnitPrice, int Quantity)[] lines)
    {
        var quote = Quote.Create(Guid.NewGuid(), 1, 0, DateTimeOffset.UtcNow.AddDays(10), Guid.NewGuid());
        quote.SetDraftDetails(
            lines.Select(line => QuoteItem.Create(Guid.NewGuid(), "Crane", line.Quantity, line.UnitPrice, "Per Visit")).ToArray(), null, null);
        return quote;
    }

    [Fact]
    public void ANewQuotationStartsWithFourteenPercentVat()
    {
        var quote = QuoteOf((100_000, 1));

        Assert.Equal(14m, quote.VatPercent);
        Assert.Equal(14_000, quote.VatPiastres);
        Assert.Equal(114_000, quote.TotalWithVatPiastres);
    }

    [Fact]
    public void TheVatIsWorkedOutOnTheTotalAfterTheDiscountAndTheAmountStaysBeforeVat()
    {
        var quote = QuoteOf((150_000, 1), (50_000, 2));
        quote.SetDiscount(10m);

        Assert.Equal(225_000, quote.AmountPiastres);
        Assert.Equal(31_500, quote.VatPiastres);
        Assert.Equal(256_500, quote.TotalWithVatPiastres);

        quote.SetVat(0m);

        Assert.Equal(225_000, quote.AmountPiastres);
        Assert.Equal(0, quote.VatPiastres);
        Assert.Equal(225_000, quote.TotalWithVatPiastres);
    }

    [Theory]
    [InlineData(1, 50, 1)]
    [InlineData(3, 14, 0)]
    [InlineData(4, 14, 1)]
    [InlineData(1_000, 12.5, 125)]
    [InlineData(0, 14, 0)]
    public void TheVatIsRoundedToThePiastreAndAHalfGoesUp(long amount, double percent, long expected) =>
        Assert.Equal(expected, Quote.VatOf(amount, (decimal)percent));

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    [InlineData(14.123)]
    public void AVatOutsideTheRulesIsRefusedAndNothingChanges(double percent)
    {
        var quote = QuoteOf((100_000, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => Quote.ValidateVat(-1m));
        Assert.ThrowsAny<ArgumentException>(() => quote.SetVat((decimal)percent));
        Assert.Equal(14m, quote.VatPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5.5)]
    [InlineData(100)]
    public void AnyVatFromZeroToOneHundredWithTwoDecimalsIsAccepted(double percent)
    {
        var quote = QuoteOf((100_000, 1));

        quote.SetVat((decimal)percent);

        Assert.Equal((decimal)percent, quote.VatPercent);
    }

    [Fact]
    public void ASentQuotationKeepsItsVat()
    {
        var quote = QuoteOf((100_000, 1));
        quote.Send();

        Assert.Throws<InvalidOperationException>(() => quote.SetVat(0m));
        Assert.Equal(14m, quote.VatPercent);
    }
}
