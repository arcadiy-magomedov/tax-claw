using System.Text.RegularExpressions;

namespace TaxClaw.Privacy;

/// <summary>
/// Regex-based detector for structured Czech PII (rodné číslo, IBAN). Extensible by pattern. This
/// catches structured identifiers but not free-text names — so cloud redaction is best-effort; the
/// strong privacy guarantee is local mode (Ollama), where nothing leaves the machine.
/// </summary>
public sealed partial class RegexPiiDetector : IPiiDetector
{
    [GeneratedRegex(@"\b\d{6}/\d{3,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RodneCislo();

    [GeneratedRegex(@"\bCZ\d{20,22}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CzechIban();

    public IReadOnlyList<PiiSpan> Detect(string text)
    {
        var spans = new List<PiiSpan>();
        foreach (Match m in RodneCislo().Matches(text))
        {
            spans.Add(new PiiSpan("rodne_cislo", m.Value));
        }
        foreach (Match m in CzechIban().Matches(text))
        {
            spans.Add(new PiiSpan("iban", m.Value));
        }
        return spans;
    }
}
