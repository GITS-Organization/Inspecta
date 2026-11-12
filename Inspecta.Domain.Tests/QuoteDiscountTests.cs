using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

/// <summary>The discount of a quotation: a percentage taken off the total of all lines, shown under the total.</summary>
public sealed class QuoteDiscountTests
{
    private static Quote QuoteOf(params (long UnitPrice, int Quantity)[] lines)
    {
        var quote = Quote.Create(Guid.NewGuid(), 1, 0, DateTimeOffset.UtcNow.AddDays(10), Guid.NewGuid());
        quote.SetDraftDetails(
            lines.Select(line => QuoteItem.Create(Guid.NewGuid(), "Crane", line.Quantity, line.UnitPrice, "Per Visit")).ToArray(), null, null);
        return quote;
    }

    [Fact]
    public void WithoutADiscountTheAmountEqualsTheSubtotal()
    {
        var quote = QuoteOf((100_000, 2));

        Assert.Equal(0m, quote.DiscountPercent);
        Assert.Equal(0, quote.DiscountPiastres);
        Assert.Equal(200_000, quote.SubtotalPiastres);
        Assert.Equal(200_000, quote.AmountPiastres);
    }

    [Fact]
    public void TheDiscountIsTakenOffTheTotalOfAllLines()
    {
        var quote = QuoteOf((150_000, 1), (50_000, 2));

        quote.SetDiscount(10m);

        Assert.Equal(250_000, quote.SubtotalPiastres);
        Assert.Equal(25_000, quote.DiscountPiastres);
        Assert.Equal(225_000, quote.AmountPiastres);
        Assert.Equal(10m, quote.DiscountPercent);
    }

    [Theory]
    [InlineData(100_005, 10, 10_001)]   // 10 000.5 piastres: the half goes up
    [InlineData(100_001, 12.5, 12_500)] // 12 500.125 piastres: rounds down
    [InlineData(99_999, 33.33, 33_330)] // 33 329.6667 piastres: rounds up
    [InlineData(1, 50, 1)]              // half a piastre: goes up
    public void TheDiscountIsRoundedToTheNearestPiastreWithHalvesGoingUp(long subtotal, double percent, long expectedDiscount)
    {
        Assert.Equal(expectedDiscount, Quote.DiscountOf(subtotal, (decimal)percent));

        var quote = QuoteOf((subtotal, 1));
        quote.SetDiscount((decimal)percent);
        Assert.Equal(expectedDiscount, quote.DiscountPiastres);
        Assert.Equal(subtotal - expectedDiscount, quote.AmountPiastres);
    }

    [Fact]
    public void AHundredPercentMakesTheTotalZeroAndZeroTakesTheDiscountOff()
    {
        var quote = QuoteOf((120_000, 1));

        quote.SetDiscount(100m);
        Assert.Equal(0, quote.AmountPiastres);
        Assert.Equal(120_000, quote.DiscountPiastres);

        quote.SetDiscount(0m);
        Assert.Equal(120_000, quote.AmountPiastres);
        Assert.Equal(0, quote.DiscountPiastres);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(250)]
    public void ADiscountOutsideZeroToAHundredIsRefused(double percent)
    {
        var quote = QuoteOf((100_000, 1));

        Assert.ThrowsAny<ArgumentException>(() => quote.SetDiscount((decimal)percent));
        Assert.Equal(0m, quote.DiscountPercent);
        Assert.Equal(100_000, quote.AmountPiastres);
    }

    [Fact]
    public void MoreThanTwoDecimalsAreRefused()
    {
        var quote = QuoteOf((100_000, 1));

        Assert.ThrowsAny<ArgumentException>(() => quote.SetDiscount(10.123m));
    }

    [Fact]
    public void SavingTheLinesAgainKeepsTheDiscountAndFollowsTheNewTotal()
    {
        var quote = QuoteOf((100_000, 1));
        quote.SetDiscount(10m);

        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 500_000, "Per Visit")], null, null);

        Assert.Equal(500_000, quote.SubtotalPiastres);
        Assert.Equal(50_000, quote.DiscountPiastres);
        Assert.Equal(450_000, quote.AmountPiastres);
    }

    [Fact]
    public void ADiscountedQuoteCanBeSentAndThenItsDiscountIsLocked()
    {
        var quote = QuoteOf((100_000, 2));
        quote.SetDiscount(25m);

        quote.Send();

        Assert.True(quote.IsSent);
        Assert.Equal(150_000, quote.AmountPiastres);
        Assert.Throws<InvalidOperationException>(() => quote.SetDiscount(5m));
        Assert.Equal(25m, quote.DiscountPercent);
    }
}
