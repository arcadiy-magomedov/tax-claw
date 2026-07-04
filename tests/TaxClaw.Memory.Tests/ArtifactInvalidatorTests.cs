using TaxClaw.Memory;

namespace TaxClaw.Memory.Tests;

public class ArtifactInvalidatorTests
{
    [Fact]
    public void Keeps_artifacts_pinned_to_the_active_version()
    {
        var artifacts = new[]
        {
            new VersionedArtifact("r38-fn", ArtifactKind.CalcFunction, "2027.1", "25 5405/2027"),
            new VersionedArtifact("rsu-parser", ArtifactKind.DocumentParser, "2026.1", "25 5405/2026")
        };

        var valid = ArtifactInvalidator.SelectValid(artifacts, lawVersion: "2027.1", formVersion: "25 5405/2027")
            .Select(a => a.Id).ToList();

        Assert.Contains("r38-fn", valid);
        Assert.DoesNotContain("rsu-parser", valid);
    }

    [Fact]
    public void Parsers_ignore_form_version_and_match_on_law_only()
    {
        var artifacts = new[]
        {
            new VersionedArtifact("rsu-parser", ArtifactKind.DocumentParser, "2027.1", null)
        };

        var valid = ArtifactInvalidator.SelectValid(artifacts, lawVersion: "2027.1", formVersion: "25 5405/2027");

        Assert.Single(valid);
    }
}
