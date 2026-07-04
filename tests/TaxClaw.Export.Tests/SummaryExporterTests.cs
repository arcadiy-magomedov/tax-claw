using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using TaxClaw.Export;

namespace TaxClaw.Export.Tests;

public class SummaryExporterTests
{
    private static TaxReturn SampleReturn()
    {
        var trace = new CalculationTrace("r38",
            new[]
            {
                new CalculationStep("read r36", "120000"),
                new CalculationStep("subtract r37", "120000 - 20000 = 100000")
            },
            "100000",
            new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));

        return new TaxReturn(TaxYear.Of(2027)).WithLine("r38", 100000m, trace);
    }

    [Fact]
    public void Summary_includes_line_value_steps_and_citation()
    {
        string md = new SummaryExporter().Export(SampleReturn());

        Assert.Contains("r38", md);
        Assert.Contains("100000", md);
        Assert.Contains("subtract r37", md);
        Assert.Contains("§ 16", md);
    }

    [Fact]
    public void Summary_states_it_is_not_tax_advice()
    {
        string md = new SummaryExporter().Export(SampleReturn());
        Assert.Contains("not tax advice", md, StringComparison.OrdinalIgnoreCase);
    }
}
