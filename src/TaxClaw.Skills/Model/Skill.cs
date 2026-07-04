namespace TaxClaw.Skills.Model;

/// <summary>A loaded skill: its manifest plus the artifact files (path → content).</summary>
public sealed record Skill(SkillManifest Manifest, IReadOnlyDictionary<string, string> Files);
