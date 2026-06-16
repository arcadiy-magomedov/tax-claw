using TaxClaw.Calc.Form;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class FormDefinitionTests
{
    [Fact]
    public void Evaluation_order_respects_dependencies()
    {
        var form = new FormDefinition("25 5405", "2027.1", new[]
        {
            new FormLineDefinition("r38", new[] { "r36", "r37" }),
            new FormLineDefinition("r36", Array.Empty<string>()),
            new FormLineDefinition("r37", Array.Empty<string>())
        });

        var order = form.EvaluationOrder().Select(l => l.Id).ToList();

        Assert.True(order.IndexOf("r36") < order.IndexOf("r38"));
        Assert.True(order.IndexOf("r37") < order.IndexOf("r38"));
    }

    [Fact]
    public void A_dependency_cycle_is_rejected()
    {
        var form = new FormDefinition("x", "1", new[]
        {
            new FormLineDefinition("a", new[] { "b" }),
            new FormLineDefinition("b", new[] { "a" })
        });

        Assert.Throws<InvalidOperationException>(() => form.EvaluationOrder());
    }
}
