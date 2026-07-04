using TaxClaw.Privacy;

namespace TaxClaw.Privacy.Tests;

public class RegexPiiDetectorTests
{
    private readonly RegexPiiDetector _detector = new();

    [Fact]
    public void Detects_rodne_cislo()
    {
        var spans = _detector.Detect("My rodné číslo is 900101/1234 today.");
        Assert.Contains(spans, s => s.Kind == "rodne_cislo" && s.Value == "900101/1234");
    }

    [Fact]
    public void Detects_czech_iban()
    {
        var spans = _detector.Detect("Refund to CZ6508000000192000145399 please.");
        Assert.Contains(spans, s => s.Kind == "iban");
    }

    [Fact]
    public void Returns_nothing_for_clean_text()
    {
        Assert.Empty(_detector.Detect("How are RSUs taxed in Czechia?"));
    }
}
