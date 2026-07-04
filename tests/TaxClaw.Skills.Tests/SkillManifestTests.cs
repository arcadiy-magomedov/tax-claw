using TaxClaw.Skills.Model;

namespace TaxClaw.Skills.Tests;

public class SkillManifestTests
{
    [Fact]
    public void Content_hash_changes_with_content()
    {
        var a = new Dictionary<string, string> { ["x"] = "1" };
        var b = new Dictionary<string, string> { ["x"] = "2" };
        Assert.NotEqual(SkillManifest.ComputeContentHash(a), SkillManifest.ComputeContentHash(b));
    }

    [Fact]
    public void File_order_does_not_affect_the_hash()
    {
        var a = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
        var b = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };
        Assert.Equal(SkillManifest.ComputeContentHash(a), SkillManifest.ComputeContentHash(b));
    }
}
