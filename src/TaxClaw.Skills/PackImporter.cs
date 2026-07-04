using System.Text.Json;
using TaxClaw.Skills.Model;
using TaxClaw.Storage;

namespace TaxClaw.Skills;

public enum ImportOutcome { PendingApproval, RejectedHashMismatch, RejectedPii }

/// <summary>The result of an import attempt and where (if anywhere) the pack was staged.</summary>
public sealed record ImportResult(ImportOutcome Outcome, string? StagedPath);

/// <summary>
/// Imports a foreign pack through the safe path: verify the content hash, refuse on PII, and stage
/// into a <c>skills-pending</c> area. Nothing becomes an active skill (and no code runs) until a
/// human approves it — approval (sandbox + tests) is handled by the calc/scripting approval gate.
/// </summary>
public sealed class PackImporter(StorageRoot root)
{
    private readonly PiiScanner _scanner = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public ImportResult Import(string packPath)
    {
        var pack = JsonSerializer.Deserialize<ExportedPack>(File.ReadAllText(packPath))
            ?? throw new InvalidOperationException($"Unreadable pack '{packPath}'.");

        string actual = SkillManifest.ComputeContentHash(pack.Files);
        if (actual != pack.Manifest.ContentHash)
        {
            return new ImportResult(ImportOutcome.RejectedHashMismatch, null);
        }

        if (_scanner.Scan(pack.Files).Count > 0)
        {
            return new ImportResult(ImportOutcome.RejectedPii, null);
        }

        string pendingDir = Path.Combine(root.Path, "skills-pending", pack.Manifest.Id);
        Directory.CreateDirectory(pendingDir);
        File.WriteAllText(Path.Combine(pendingDir, "skill.json"),
            JsonSerializer.Serialize(pack.Manifest, Json));
        File.WriteAllText(Path.Combine(pendingDir, "contents.json"),
            JsonSerializer.Serialize(pack.Files, Json));

        return new ImportResult(ImportOutcome.PendingApproval, pendingDir);
    }
}
