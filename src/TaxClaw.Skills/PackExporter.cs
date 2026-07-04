using System.Text.Json;
using TaxClaw.Skills.Model;

namespace TaxClaw.Skills;

/// <summary>A self-contained, shareable pack: manifest + artifact files.</summary>
public sealed record ExportedPack(SkillManifest Manifest, IReadOnlyDictionary<string, string> Files);

/// <summary>
/// Bundles shareable artifacts into a single pack file, but only after the PII scanner is clear.
/// This is the safe outbound boundary; the git repo is just where the resulting file is shared.
/// </summary>
public sealed class PackExporter
{
    private readonly PiiScanner _scanner = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public void Export(
        string path, string id, string version, string lawVersion, string? formVersion,
        string author, IReadOnlyDictionary<string, string> files)
    {
        var findings = _scanner.Scan(files);
        if (findings.Count > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to export: PII detected in {findings.Count} place(s), first in '{findings[0].File}'.");
        }

        var manifest = new SkillManifest(
            id, version, lawVersion, formVersion, author,
            SkillManifest.ComputeContentHash(files));

        var pack = new ExportedPack(manifest, files);
        File.WriteAllText(path, JsonSerializer.Serialize(pack, Json));
    }
}
