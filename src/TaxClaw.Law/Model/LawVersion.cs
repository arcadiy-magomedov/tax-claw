namespace TaxClaw.Law.Model;

/// <summary>
/// A dated edition (consolidated wording) of an act, addressable by ELI on e-Sbírka. Selecting the
/// edition by the project's tax year implements "law as of the tax year".
/// </summary>
public sealed record LawVersion(string ActNumber, DateOnly EffectiveOn)
{
    /// <summary>ELI path: act "586/1992" on 2027-01-01 → "eli/cz/sb/1992/586/2027-01-01".</summary>
    public string Eli
    {
        get
        {
            string[] parts = ActNumber.Split('/');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                throw new FormatException($"ActNumber '{ActNumber}' must be 'number/year', e.g. '586/1992'.");
            }
            return $"eli/cz/sb/{parts[1]}/{parts[0]}/{EffectiveOn:yyyy-MM-dd}";
        }
    }
}
