using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class MathToolsTests
{
    [Fact]
    public void Add_uses_exact_decimal_arithmetic()
    {
        Assert.Equal(0.3m, MathTools.Add(0.1m, 0.2m));
    }

    [Fact]
    public void CreateTools_exposes_the_expected_named_functions()
    {
        var names = MathTools.CreateTools()
            .OfType<AIFunction>()
            .Select(f => f.Name)
            .ToHashSet();

        Assert.Superset(
            new HashSet<string> { "add", "subtract", "multiply", "divide", "round_to_unit" },
            names);
    }
}
