using TaxClaw.Calc;
using TaxClaw.Calc.Form;
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class CalcEngineTests
{
    private sealed class ConstFunction(decimal value) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            steps = new[] { new CalculationStep("const", value.ToString()) };
            return value;
        }
    }

    private sealed class SubtractFunction(string a, string b) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            decimal result = context.Subtract(context.Line(a), context.Line(b));
            steps = new[]
            {
                new CalculationStep($"{a} - {b}", $"{context.Line(a)} - {context.Line(b)} = {result}")
            };
            return result;
        }
    }

    [Fact]
    public void Computes_dependent_line_and_records_traces()
    {
        var form = new FormDefinition("25 5405", "2027.1", new[]
        {
            new FormLineDefinition("r36", Array.Empty<string>()),
            new FormLineDefinition("r37", Array.Empty<string>()),
            new FormLineDefinition("r38", new[] { "r36", "r37" })
        });

        var registry = new CalcFunctionRegistry();
        registry.Register(new CalcFunctionKey("r36", "2027.1"), new ConstFunction(120000m));
        registry.Register(new CalcFunctionKey("r37", "2027.1"), new ConstFunction(20000m));
        registry.Register(new CalcFunctionKey("r38", "2027.1"), new SubtractFunction("r36", "r37"));

        var engine = new CalcEngine(registry);
        TaxReturn result = engine.Evaluate(form, new TaxReturn(TaxYear.Of(2027)));

        Assert.Equal(100000m, result.GetLine("r38"));
        Assert.Equal("r38", result.GetTrace("r38")!.LineId);
        Assert.Equal("2027.1", result.GetTrace("r38")!.Provenance.Version);
    }

    [Fact]
    public void Missing_function_for_a_line_throws_with_the_line_id()
    {
        var form = new FormDefinition("x", "2027.1", new[]
        {
            new FormLineDefinition("r1", Array.Empty<string>())
        });

        var engine = new CalcEngine(new CalcFunctionRegistry());

        var ex = Assert.Throws<InvalidOperationException>(
            () => engine.Evaluate(form, new TaxReturn(TaxYear.Of(2027))));
        Assert.Contains("r1", ex.Message);
    }
}
