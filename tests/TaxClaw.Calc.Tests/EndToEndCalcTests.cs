using TaxClaw.Calc;
using TaxClaw.Calc.Form;
using TaxClaw.Calc.Functions;
using TaxClaw.Calc.Scripting;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class EndToEndCalcTests
{
    private sealed class ConstFunction(decimal value) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            steps = new[] { new CalculationStep("input", value.ToString()) };
            return value;
        }
    }

    [Fact]
    public async Task Approved_generated_subtraction_computes_a_line_with_trace()
    {
        // Approve + compile a generated function for r38.
        var gate = new ApprovalGate();
        var src = new FunctionSource(
            "r38", "2027.1",
            "return ctx.Subtract(ctx.Line(\"r36\"), ctx.Line(\"r37\"));",
            new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));
        gate.Approve(src);
        ICalcFunction r38 = await new ScriptCompiler(gate).CompileAsync(src);

        var registry = new CalcFunctionRegistry();
        registry.Register(new CalcFunctionKey("r36", "2027.1"), new ConstFunction(120000m));
        registry.Register(new CalcFunctionKey("r37", "2027.1"), new ConstFunction(20000m));
        registry.Register(new CalcFunctionKey("r38", "2027.1"), r38);

        var form = new FormDefinition("25 5405", "2027.1", new[]
        {
            new FormLineDefinition("r36", Array.Empty<string>()),
            new FormLineDefinition("r37", Array.Empty<string>()),
            new FormLineDefinition("r38", new[] { "r36", "r37" })
        });

        TaxReturn result = new CalcEngine(registry).Evaluate(form, new TaxReturn(TaxYear.Of(2027)));

        Assert.Equal(100000m, result.GetLine("r38"));
        Assert.Equal("r38", result.GetTrace("r38")!.LineId);
    }
}
