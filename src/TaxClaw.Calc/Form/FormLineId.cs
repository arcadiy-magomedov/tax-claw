namespace TaxClaw.Calc.Form;

/// <summary>The identifier of a form line (řádek), e.g. "r38".</summary>
public readonly record struct FormLineId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator string(FormLineId id) => id.Value;
}
