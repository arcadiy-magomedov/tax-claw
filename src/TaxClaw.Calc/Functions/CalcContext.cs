using TaxClaw.Core.Math;

namespace TaxClaw.Calc.Functions;

/// <summary>
/// What a calc function may touch while computing a line: previously computed line values and
/// the sanctioned decimal math. Functions never reach outside this surface.
/// </summary>
public sealed class CalcContext(IReadOnlyDictionary<string, decimal> lines)
{
    public decimal Line(string id) =>
        lines.TryGetValue(id, out var v)
            ? v
            : throw new KeyNotFoundException($"Line '{id}' has not been computed yet.");

    public decimal Add(decimal a, decimal b) => DecimalMath.Add(a, b);
    public decimal Subtract(decimal a, decimal b) => DecimalMath.Subtract(a, b);
    public decimal Multiply(decimal a, decimal b) => DecimalMath.Multiply(a, b);
    public decimal Divide(decimal a, decimal b) => DecimalMath.Divide(a, b);
    public decimal RoundToUnit(decimal value, decimal unit, RoundingDirection direction) =>
        DecimalMath.RoundToUnit(value, unit, direction);
}
