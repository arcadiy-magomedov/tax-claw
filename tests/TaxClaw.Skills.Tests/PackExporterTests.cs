using System.Text.Json;
using TaxClaw.Skills;
using TaxClaw.Skills.Model;

namespace TaxClaw.Skills.Tests;

public class PackExporterTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "taxclaw-pack-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Exports_clean_artifacts_to_a_pack_file()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "rsu.pack.json");

        var files = new Dictionary<string, string> { ["rules.md"] = "RSU is § 6 for any employer." };
        new PackExporter().Export(path, "rsu-generic", "1.0", "2027.1", "25 5405/2027", "alice", files);

        Assert.True(File.Exists(path));
        var pack = JsonSerializer.Deserialize<ExportedPack>(File.ReadAllText(path))!;
        Assert.Equal("rsu-generic", pack.Manifest.Id);
        Assert.Equal(SkillManifest.ComputeContentHash(files), pack.Manifest.ContentHash);
    }

    [Fact]
    public void Refuses_to_export_when_pii_is_present()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "bad.pack.json");

        var files = new Dictionary<string, string> { ["rules.md"] = "taxpayer 900101/1234" };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PackExporter().Export(path, "bad", "1.0", "2027.1", null, "alice", files));

        Assert.Contains("PII", ex.Message);
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
