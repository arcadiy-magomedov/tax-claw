using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Core.Tests;

public class CalculationTraceTests
{
    [Fact]
    public void Trace_renders_steps_in_order_for_explanation()
    {
        var trace = new CalculationTrace("r38", new[]
        {
            new CalculationStep("read r36", "120000"),
            new CalculationStep("subtract r37", "120000 - 20000 = 100000")
        }, "100000", new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1", Hash: "abc"));

        Assert.Equal("r38", trace.LineId);
        Assert.Equal("100000", trace.Result);
        Assert.Equal(2, trace.Steps.Count);
        Assert.Contains("§ 16", trace.Provenance.ToString());
    }
}
