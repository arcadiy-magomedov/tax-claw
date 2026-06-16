namespace TaxClaw.Core.Model;

/// <summary>A Czech tax year that a declaration project targets.</summary>
public sealed record TaxYear(int Year)
{
    public static TaxYear Of(int year) =>
        year is >= 2000 and <= 2100
            ? new TaxYear(year)
            : throw new ArgumentOutOfRangeException(nameof(year), year, "Tax year must be between 2000 and 2100.");

    public override string ToString() => Year.ToString();
}
