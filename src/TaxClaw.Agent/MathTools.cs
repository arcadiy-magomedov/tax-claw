using System.ComponentModel;
using Microsoft.Extensions.AI;
using TaxClaw.Core.Math;

namespace TaxClaw.Agent;

/// <summary>
/// The only sanctioned way for the agent to produce a number on the fly. Each tool is a thin,
/// deterministic wrapper over <see cref="DecimalMath"/> — the model must call these instead of
/// doing arithmetic itself.
/// </summary>
public static class MathTools
{
    [Description("Add two exact decimal numbers and return their sum.")]
    public static decimal Add(decimal a, decimal b) => DecimalMath.Add(a, b);

    [Description("Subtract b from a using exact decimal arithmetic.")]
    public static decimal Subtract(decimal a, decimal b) => DecimalMath.Subtract(a, b);

    [Description("Multiply two exact decimal numbers.")]
    public static decimal Multiply(decimal a, decimal b) => DecimalMath.Multiply(a, b);

    [Description("Divide a by b using exact decimal arithmetic. Errors if b is zero.")]
    public static decimal Divide(decimal a, decimal b) => DecimalMath.Divide(a, b);

    [Description("Round a value to the nearest multiple of unit. direction is 'Up', 'Down', or 'Nearest'.")]
    public static decimal RoundToUnit(decimal value, decimal unit, RoundingDirection direction) =>
        DecimalMath.RoundToUnit(value, unit, direction);

    /// <summary>Builds the tool list passed to the agent's chat options.</summary>
    public static IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(Add, name: "add"),
        AIFunctionFactory.Create(Subtract, name: "subtract"),
        AIFunctionFactory.Create(Multiply, name: "multiply"),
        AIFunctionFactory.Create(Divide, name: "divide"),
        AIFunctionFactory.Create(RoundToUnit, name: "round_to_unit")
    ];
}
