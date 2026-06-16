using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class CalcFunctionRegistryTests
{
    private sealed class ConstFunction(decimal value) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            steps = new[] { new CalculationStep("const", value.ToString()) };
            return value;
        }
    }

    [Fact]
    public void Resolves_a_registered_function_by_line_and_version()
    {
        var registry = new CalcFunctionRegistry();
        var key = new CalcFunctionKey("r36", "2027.1");
        registry.Register(key, new ConstFunction(120000m));

        Assert.True(registry.TryResolve(key, out var fn));
        Assert.NotNull(fn);
    }

    [Fact]
    public void A_different_version_does_not_resolve()
    {
        var registry = new CalcFunctionRegistry();
        registry.Register(new CalcFunctionKey("r36", "2027.1"), new ConstFunction(1m));

        Assert.False(registry.TryResolve(new CalcFunctionKey("r36", "2026.1"), out _));
    }
}
