using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Core.Tests;

public class TaxReturnTests
{
    [Fact]
    public void Setting_a_line_stores_value_and_trace()
    {
        var ret = new TaxReturn(TaxYear.Of(2027));
        var trace = new CalculationTrace("r38",
            new[] { new CalculationStep("x", "y") }, "100000",
            new Provenance(FormLine: "r38"));

        ret = ret.WithLine("r38", 100000m, trace);

        Assert.Equal(100000m, ret.GetLine("r38"));
        Assert.Equal(trace, ret.GetTrace("r38"));
    }

    [Fact]
    public void Getting_an_unset_line_returns_null()
    {
        var ret = new TaxReturn(TaxYear.Of(2027));
        Assert.Null(ret.GetLine("r99"));
    }
}
