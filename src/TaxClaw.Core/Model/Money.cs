using TaxClaw.Core.Math;

namespace TaxClaw.Core.Model;

/// <summary>A currency-tagged exact decimal amount. All arithmetic stays in decimal.</summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public string Currency { get; } = Currency.ToUpperInvariant();

    public static Money Czk(decimal amount) => new(amount, "CZK");

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = DecimalMath.Add(Amount, other.Amount) };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = DecimalMath.Subtract(Amount, other.Amount) };
    }

    public Money Multiply(decimal factor) =>
        this with { Amount = DecimalMath.Multiply(Amount, factor) };

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine {Currency} with {other.Currency}; convert first.");
        }
    }

    public override string ToString() => $"{Amount} {Currency}";
}
