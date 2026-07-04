using System.Text.Json;
using TaxClaw.Skills;
using TaxClaw.Skills.Model;
using TaxClaw.Storage;

namespace TaxClaw.Skills.Tests;

public class PackImporterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-import-" + Guid.NewGuid().ToString("N"));

    private string WritePack(string id, IReadOnlyDictionary<string, string> files, string? overrideHash = null)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, id + ".pack.json");
        var manifest = new SkillManifest(id, "1.0", "2027.1", "25 5405/2027", "bob",
            overrideHash ?? SkillManifest.ComputeContentHash(files));
        File.WriteAllText(path, JsonSerializer.Serialize(new ExportedPack(manifest, files)));
        return path;
    }

    [Fact]
    public void Imports_a_valid_pack_into_the_pending_area_not_active_skills()
    {
        var files = new Dictionary<string, string> { ["rules.md"] = "RSU is § 6." };
        string packPath = WritePack("rsu-generic", files);

        ImportResult result = new PackImporter(new StorageRoot(_root)).Import(packPath);

        Assert.Equal(ImportOutcome.PendingApproval, result.Outcome);
        Assert.True(Directory.Exists(Path.Combine(new StorageRoot(_root).Path, "skills-pending", "rsu-generic")));
        Assert.False(Directory.Exists(Path.Combine(new StorageRoot(_root).Path, "skills", "rsu-generic")));
    }

    [Fact]
    public void Rejects_a_pack_with_a_bad_hash()
    {
        string packPath = WritePack("tampered",
            new Dictionary<string, string> { ["rules.md"] = "real" }, overrideHash: "deadbeef");

        var result = new PackImporter(new StorageRoot(_root)).Import(packPath);

        Assert.Equal(ImportOutcome.RejectedHashMismatch, result.Outcome);
    }

    [Fact]
    public void Rejects_a_pack_that_contains_pii()
    {
        string packPath = WritePack("bad",
            new Dictionary<string, string> { ["rules.md"] = "taxpayer 900101/1234" });

        var result = new PackImporter(new StorageRoot(_root)).Import(packPath);

        Assert.Equal(ImportOutcome.RejectedPii, result.Outcome);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
