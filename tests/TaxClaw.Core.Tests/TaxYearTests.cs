using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Core.Tests;

public class TaxYearTests
{
    [Fact]
    public void Of_accepts_a_valid_year()
    {
        var year = TaxYear.Of(2027);
        Assert.Equal(2027, year.Year);
        Assert.Equal("2027", year.ToString());
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Of_rejects_out_of_range_years(int invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxYear.Of(invalid));
    }
}
