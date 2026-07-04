using System.Text.Json;
using TaxClaw.Skills;
using TaxClaw.Skills.Model;
using TaxClaw.Storage;

namespace TaxClaw.Skills.Tests;

public class SkillLoaderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-skills-" + Guid.NewGuid().ToString("N"));

    private void WriteSkill(string id, Dictionary<string, string> files, string? overrideHash = null)
    {
        string dir = Path.Combine(new StorageRoot(_root).Path, "skills", id);
        Directory.CreateDirectory(dir);

        var manifest = new SkillManifest(id, "1.0", "2027.1", "25 5405/2027", "tester",
            overrideHash ?? SkillManifest.ComputeContentHash(files));

        File.WriteAllText(Path.Combine(dir, "skill.json"), JsonSerializer.Serialize(manifest));
        File.WriteAllText(Path.Combine(dir, "contents.json"), JsonSerializer.Serialize(files));
    }

    [Fact]
    public void Loads_a_valid_skill()
    {
        WriteSkill("rsu-msft", new Dictionary<string, string> { ["rules.md"] = "RSU -> § 6" });

        var skills = new SkillLoader(new StorageRoot(_root)).LoadInstalled();

        Assert.Single(skills);
        Assert.Equal("rsu-msft", skills[0].Manifest.Id);
    }

    [Fact]
    public void Rejects_a_skill_whose_content_hash_does_not_match()
    {
        WriteSkill("tampered",
            new Dictionary<string, string> { ["rules.md"] = "real" },
            overrideHash: "deadbeef");

        Assert.Throws<InvalidOperationException>(
            () => new SkillLoader(new StorageRoot(_root)).LoadInstalled());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
