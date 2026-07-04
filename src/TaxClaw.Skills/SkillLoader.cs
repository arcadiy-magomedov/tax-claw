using System.Text.Json;
using TaxClaw.Skills.Model;
using TaxClaw.Storage;

namespace TaxClaw.Skills;

/// <summary>
/// Discovers installed skills under <c>{root}/skills/*</c> and validates each one's content hash so
/// tampered or corrupted skills are refused before any artifact is used.
/// </summary>
public sealed class SkillLoader(StorageRoot root)
{
    private string SkillsDir => Path.Combine(root.Path, "skills");

    public IReadOnlyList<Skill> LoadInstalled()
    {
        if (!Directory.Exists(SkillsDir))
        {
            return [];
        }

        var skills = new List<Skill>();
        foreach (string dir in Directory.GetDirectories(SkillsDir))
        {
            string manifestPath = Path.Combine(dir, "skill.json");
            string contentsPath = Path.Combine(dir, "contents.json");
            if (!File.Exists(manifestPath) || !File.Exists(contentsPath))
            {
                continue;
            }

            var manifest = JsonSerializer.Deserialize<SkillManifest>(File.ReadAllText(manifestPath))
                ?? throw new InvalidOperationException($"Unreadable manifest in '{dir}'.");
            var files = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(contentsPath))
                ?? throw new InvalidOperationException($"Unreadable contents in '{dir}'.");

            string actual = SkillManifest.ComputeContentHash(files);
            if (actual != manifest.ContentHash)
            {
                throw new InvalidOperationException(
                    $"Skill '{manifest.Id}' content hash mismatch (expected {manifest.ContentHash}, got {actual}).");
            }

            skills.Add(new Skill(manifest, files));
        }

        return skills;
    }
}
