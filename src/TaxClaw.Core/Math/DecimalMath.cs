namespace TaxClaw.Core.Math;

/// <summary>Direction used when snapping a value to a rounding unit.</summary>
public enum RoundingDirection
{
    Up,
    Down,
    Nearest
}

/// <summary>
/// Deterministic, exact decimal arithmetic. This is the ONLY sanctioned way for the
/// application (and the agent) to produce numbers — never binary floating point.
/// </summary>
public static class DecimalMath
{
    public static decimal Add(decimal a, decimal b) => a + b;

    public static decimal Subtract(decimal a, decimal b) => a - b;

    public static decimal Multiply(decimal a, decimal b) => a * b;

    public static decimal Divide(decimal a, decimal b)
    {
        if (b == 0m)
        {
            throw new DivideByZeroException("Division by zero is not allowed.");
        }

        return a / b;
    }

    /// <summary>
    /// Snaps <paramref name="value"/> to the nearest multiple of <paramref name="unit"/>
    /// in the requested <paramref name="direction"/>. Pure arithmetic — tax-specific
    /// conventions (which unit, which direction) live in <see cref="CzechTaxRounding"/>.
    /// </summary>
    public static decimal RoundToUnit(decimal value, decimal unit, RoundingDirection direction)
    {
        if (unit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be positive.");
        }

        decimal quotient = value / unit;
        decimal rounded = direction switch
        {
            RoundingDirection.Up => decimal.Ceiling(quotient),
            RoundingDirection.Down => decimal.Floor(quotient),
            RoundingDirection.Nearest => decimal.Round(quotient, 0, MidpointRounding.AwayFromZero),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown rounding direction.")
        };

        return rounded * unit;
    }
}
