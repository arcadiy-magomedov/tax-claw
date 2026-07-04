using TaxClaw.Skills;

namespace TaxClaw.Skills.Tests;

public class PiiScannerTests
{
    private readonly PiiScanner _scanner = new();

    [Fact]
    public void Flags_a_rodne_cislo_pattern()
    {
        var findings = _scanner.Scan(new Dictionary<string, string>
        {
            ["rules.md"] = "Applies to taxpayer 900101/1234 only."
        });

        Assert.NotEmpty(findings);
        Assert.Equal("rules.md", findings[0].File);
    }

    [Fact]
    public void Flags_an_iban_like_account_number()
    {
        var findings = _scanner.Scan(new Dictionary<string, string>
        {
            ["x"] = "Refund to CZ6508000000192000145399"
        });

        Assert.NotEmpty(findings);
    }

    [Fact]
    public void Clean_generalized_artifacts_have_no_findings()
    {
        var findings = _scanner.Scan(new Dictionary<string, string>
        {
            ["calc/r38.csx"] = "return ctx.Subtract(ctx.Line(\"r36\"), ctx.Line(\"r37\"));",
            ["rules.md"] = "RSU from any employer is § 6 employment income."
        });

        Assert.Empty(findings);
    }
}
