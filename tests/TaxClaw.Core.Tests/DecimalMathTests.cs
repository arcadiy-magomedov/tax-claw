using TaxClaw.Core.Math;
using Xunit;

namespace TaxClaw.Core.Tests;

public class DecimalMathTests
{
    [Fact]
    public void Add_is_exact_for_decimals()
    {
        Assert.Equal(0.3m, DecimalMath.Add(0.1m, 0.2m));
    }

    [Fact]
    public void Subtract_and_multiply_are_exact()
    {
        Assert.Equal(0.2m, DecimalMath.Subtract(0.5m, 0.3m));
        Assert.Equal(6.25m, DecimalMath.Multiply(2.5m, 2.5m));
    }

    [Fact]
    public void Divide_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => DecimalMath.Divide(1m, 0m));
    }

    [Theory]
    [InlineData("12345", "100", "Down", "12300")]
    [InlineData("1234.01", "1", "Up", "1235")]
    [InlineData("1250", "100", "Nearest", "1300")]
    public void RoundToUnit_rounds_in_the_requested_direction(
        string value, string unit, string direction, string expected)
    {
        var result = DecimalMath.RoundToUnit(
            decimal.Parse(value),
            decimal.Parse(unit),
            Enum.Parse<RoundingDirection>(direction));

        Assert.Equal(decimal.Parse(expected), result);
    }

    [Fact]
    public void RoundToUnit_rejects_non_positive_unit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DecimalMath.RoundToUnit(100m, 0m, RoundingDirection.Down));
    }
}
