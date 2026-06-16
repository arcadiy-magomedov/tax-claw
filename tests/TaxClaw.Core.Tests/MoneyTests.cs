using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Core.Tests;

public class MoneyTests
{
    [Fact]
    public void Adding_same_currency_sums_amounts()
    {
        var sum = Money.Czk(100m).Add(Money.Czk(50m));
        Assert.Equal(Money.Czk(150m), sum);
    }

    [Fact]
    public void Adding_different_currencies_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Money.Czk(100m).Add(new Money(10m, "USD")));
    }

    [Fact]
    public void Currency_is_normalized_to_upper_case()
    {
        Assert.Equal("USD", new Money(1m, "usd").Currency);
    }
}
