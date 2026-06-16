using TaxClaw.Core.Math;
using Xunit;

namespace TaxClaw.Core.Tests;

public class CzechTaxRoundingTests
{
    [Theory]
    [InlineData("156789", "156700")]
    [InlineData("156700", "156700")]
    [InlineData("99", "0")]
    public void Tax_base_is_rounded_down_to_whole_hundreds(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected),
            CzechTaxRounding.TaxBaseToHundredsDown(decimal.Parse(input)));
    }

    [Theory]
    [InlineData("1234.01", "1235")]
    [InlineData("1234.00", "1234")]
    [InlineData("0.01", "1")]
    public void Tax_is_rounded_up_to_whole_crowns(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected),
            CzechTaxRounding.TaxToWholeCrownsUp(decimal.Parse(input)));
    }
}
