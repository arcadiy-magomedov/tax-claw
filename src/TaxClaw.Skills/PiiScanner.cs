using System.Text.RegularExpressions;

namespace TaxClaw.Skills;

/// <summary>A potential PII occurrence found in pack content.</summary>
public readonly record struct PiiFinding(string File, string Kind, string Sample);

/// <summary>
/// Scans pack files for obvious personal data before sharing. This is defense-in-depth on top of
/// the architectural split that already keeps PII out of shareable artifacts — it catches
/// structured identifiers (rodné číslo, Czech IBAN) but is not a complete detector (free-text names
/// can slip through); the real guarantee is that documents/amounts are never in shareable packs.
/// </summary>
public sealed partial class PiiScanner
{
    [GeneratedRegex(@"\b\d{6}/\d{3,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RodneCislo();

    [GeneratedRegex(@"\bCZ\d{2}\d{4}\d{6}\d{10}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CzechIban();

    public IReadOnlyList<PiiFinding> Scan(IReadOnlyDictionary<string, string> files)
    {
        var findings = new List<PiiFinding>();

        foreach ((string file, string content) in files)
        {
            foreach (Match m in RodneCislo().Matches(content))
            {
                findings.Add(new PiiFinding(file, "rodne_cislo", m.Value));
            }
            foreach (Match m in CzechIban().Matches(content))
            {
                findings.Add(new PiiFinding(file, "iban", m.Value));
            }
        }

        return findings;
    }
}
