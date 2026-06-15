# tax-claw Skills, MCP & Sharing — Implementation Plan (Plan 6)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make know-how packageable and shareable. Define a skill/knowledge-pack format (manifest + artifacts), load installed skills, export a pack from local artifacts with a PII scan, and import a foreign pack only through a safe path (sandbox + tests + explicit approval + version pin) — with no PII ever leaving by construction.

**Architecture:** A new `TaxClaw.Skills` library. A `SkillManifest` (id, version, pinned law/form version, author, content hash) plus a folder of artifacts is a `Skill`. `SkillLoader` discovers and validates installed skills under the data root. `PackExporter` bundles only the shareable artifacts after a `PiiScanner` clears them. `PackImporter` verifies the manifest hash, refuses on PII, and stages the pack as *pending approval* — nothing executes until approved (reusing Plan 2's `ApprovalGate`). A separate `TaxClaw.Mcp` library exposes the internal tools over MCP and can connect external MCP servers.

**Tech Stack:** .NET 10, `System.Text.Json`, `ModelContextProtocol` (MCP C# SDK), xUnit. Builds on Plan 1 (`StorageRoot`), Plan 2 (`ApprovalGate`, `FunctionSource`), Plan 5 (`VersionedArtifact`).

---

## File Structure

- `src/TaxClaw.Skills/Model/SkillManifest.cs` — manifest record + hashing.
- `src/TaxClaw.Skills/Model/Skill.cs` — manifest + artifact file list.
- `src/TaxClaw.Skills/PiiScanner.cs` — detect PII in pack contents.
- `src/TaxClaw.Skills/SkillLoader.cs` — discover + validate installed skills.
- `src/TaxClaw.Skills/PackExporter.cs` — build a shareable pack (PII-gated).
- `src/TaxClaw.Skills/PackImporter.cs` — verify + stage pending approval.
- `src/TaxClaw.Mcp/TaxClawMcpServer.cs` — expose internal tools over MCP.
- Tests under `tests/TaxClaw.Skills.Tests/`.

---

### Task 1: Scaffold the skills and MCP libraries

**Files:**
- Create: `src/TaxClaw.Skills`, `src/TaxClaw.Mcp`, `tests/TaxClaw.Skills.Tests`

- [ ] **Step 1: Create and reference projects**

```bash
dotnet new classlib -o src/TaxClaw.Skills
dotnet new classlib -o src/TaxClaw.Mcp
dotnet new xunit    -o tests/TaxClaw.Skills.Tests
rm src/TaxClaw.Skills/Class1.cs src/TaxClaw.Mcp/Class1.cs tests/TaxClaw.Skills.Tests/UnitTest1.cs

dotnet sln add src/TaxClaw.Skills src/TaxClaw.Mcp tests/TaxClaw.Skills.Tests

dotnet add src/TaxClaw.Skills reference src/TaxClaw.Core src/TaxClaw.Storage src/TaxClaw.Calc.Scripting src/TaxClaw.Memory
dotnet add src/TaxClaw.Mcp reference src/TaxClaw.Agent
dotnet add src/TaxClaw.Mcp package ModelContextProtocol
dotnet add tests/TaxClaw.Skills.Tests reference src/TaxClaw.Core src/TaxClaw.Storage src/TaxClaw.Skills
```

- [ ] **Step 2: Verify build, then commit**

Run: `dotnet build`
Expected: `Build succeeded.`

```bash
git add -A
git commit -m "chore(skills): scaffold skills and mcp libraries"
```

---

### Task 2: Skill manifest with content hashing

**Files:**
- Create: `src/TaxClaw.Skills/Model/SkillManifest.cs`
- Test: `tests/TaxClaw.Skills.Tests/SkillManifestTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Skills.Tests/SkillManifestTests.cs`:

```csharp
using TaxClaw.Skills.Model;
using Xunit;

namespace TaxClaw.Skills.Tests;

public class SkillManifestTests
{
    [Fact]
    public void Content_hash_is_stable_for_the_same_files()
    {
        var files = new Dictionary<string, string>
        {
            ["calc/r38.csx"] = "return ctx.Subtract(ctx.Line(\"r36\"), ctx.Line(\"r37\"));",
            ["rules.md"] = "RSU MSFT -> § 6"
        };

        string h1 = SkillManifest.ComputeContentHash(files);
        string h2 = SkillManifest.ComputeContentHash(files);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Content_hash_changes_when_a_file_changes()
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: FAIL — `SkillManifest` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Skills/Model/SkillManifest.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace TaxClaw.Skills.Model;

/// <summary>
/// Identifies and pins a shareable skill / knowledge pack: what it is, which law and form versions
/// it was built against, who authored it, and a content hash binding the exact artifact bytes.
/// </summary>
public sealed record SkillManifest(
    string Id,
    string Version,
    string LawVersion,
    string? FormVersion,
    string Author,
    string ContentHash)
{
    /// <summary>Order-independent hash over the pack's file contents.</summary>
    public static string ComputeContentHash(IReadOnlyDictionary<string, string> files)
    {
        var sb = new StringBuilder();
        foreach (var kv in files.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append(kv.Key).Append('\0').Append(kv.Value).Append('\n');
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Skills/Model/SkillManifest.cs tests/TaxClaw.Skills.Tests/SkillManifestTests.cs
git commit -m "feat(skills): add skill manifest with content hashing"
```

---

### Task 3: PII scanner

**Files:**
- Create: `src/TaxClaw.Skills/PiiScanner.cs`
- Test: `tests/TaxClaw.Skills.Tests/PiiScannerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Skills.Tests/PiiScannerTests.cs`:

```csharp
using TaxClaw.Skills;
using Xunit;

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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: FAIL — `PiiScanner` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Skills/PiiScanner.cs`:

```csharp
using System.Text.RegularExpressions;

namespace TaxClaw.Skills;

/// <summary>A potential PII occurrence found in pack content.</summary>
public readonly record struct PiiFinding(string File, string Kind, string Sample);

/// <summary>
/// Scans pack files for obvious personal data before sharing. A defense-in-depth check on top of
/// the architectural split that already keeps PII out of shareable artifacts.
/// </summary>
public sealed partial class PiiScanner
{
    [GeneratedRegex(@"\b\d{6}/\d{3,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RodneCislo();

    [GeneratedRegex(@"\bCZ\d{2}\d{4}\d{6}\d{10}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CzechIban();

    public IReadOnlyList<PiiFinding> Scan(IReadOnlyDictionary<string, string> files)
    {
        var findings = new List<PiiFinding>();

        foreach ((string file, string content) in files)
        {
            foreach (Match m in RodneCislo().Matches(content))
            {
                findings.Add(new PiiFinding(file, "rodne_cislo", m.Value));
            }
            foreach (Match m in CzechIban().Matches(content))
            {
                findings.Add(new PiiFinding(file, "iban", m.Value));
            }
        }

        return findings;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: PASS — rodné číslo and IBAN flagged; clean artifacts pass.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Skills/PiiScanner.cs tests/TaxClaw.Skills.Tests/PiiScannerTests.cs
git commit -m "feat(skills): add PII scanner for pack content"
```

---

### Task 4: Skill model and loader

**Files:**
- Create: `src/TaxClaw.Skills/Model/Skill.cs`
- Create: `src/TaxClaw.Skills/SkillLoader.cs`
- Test: `tests/TaxClaw.Skills.Tests/SkillLoaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Skills.Tests/SkillLoaderTests.cs`:

```csharp
using System.Text.Json;
using TaxClaw.Skills;
using TaxClaw.Skills.Model;
using TaxClaw.Storage;
using Xunit;

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

        File.WriteAllText(Path.Combine(dir, "skill.json"),
            JsonSerializer.Serialize(manifest));
        File.WriteAllText(Path.Combine(dir, "contents.json"),
            JsonSerializer.Serialize(files));
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
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: FAIL — `Skill` / `SkillLoader` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Skills/Model/Skill.cs`:

```csharp
namespace TaxClaw.Skills.Model;

/// <summary>A loaded skill: its manifest plus the artifact files (path → content).</summary>
public sealed record Skill(SkillManifest Manifest, IReadOnlyDictionary<string, string> Files);
```

Create `src/TaxClaw.Skills/SkillLoader.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: PASS — valid skill loads; tampered one throws.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Skills/Model/Skill.cs src/TaxClaw.Skills/SkillLoader.cs tests/TaxClaw.Skills.Tests/SkillLoaderTests.cs
git commit -m "feat(skills): add skill model and validating loader"
```

---

### Task 5: Pack exporter (PII-gated)

**Files:**
- Create: `src/TaxClaw.Skills/PackExporter.cs`
- Test: `tests/TaxClaw.Skills.Tests/PackExporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Skills.Tests/PackExporterTests.cs`:

```csharp
using System.Text.Json;
using TaxClaw.Skills;
using TaxClaw.Skills.Model;
using Xunit;

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
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: FAIL — `PackExporter` / `ExportedPack` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Skills/PackExporter.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: PASS — clean export writes a pack; PII export throws and writes nothing.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Skills/PackExporter.cs tests/TaxClaw.Skills.Tests/PackExporterTests.cs
git commit -m "feat(skills): add PII-gated pack exporter"
```

---

### Task 6: Pack importer (verify + stage pending approval)

**Files:**
- Create: `src/TaxClaw.Skills/PackImporter.cs`
- Test: `tests/TaxClaw.Skills.Tests/PackImporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Skills.Tests/PackImporterTests.cs`:

```csharp
using System.Text.Json;
using TaxClaw.Skills;
using TaxClaw.Skills.Model;
using TaxClaw.Storage;
using Xunit;

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

        var importer = new PackImporter(new StorageRoot(_root));
        ImportResult result = importer.Import(packPath);

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
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: FAIL — importer types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Skills/PackImporter.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: PASS — valid pack staged as pending; bad-hash and PII packs rejected; never auto-activated.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Skills/PackImporter.cs tests/TaxClaw.Skills.Tests/PackImporterTests.cs
git commit -m "feat(skills): add pack importer that stages pending approval"
```

---

### Task 7: Expose internal tools over MCP

**Files:**
- Create: `src/TaxClaw.Mcp/TaxClawMcpServer.cs`
- Test: `tests/TaxClaw.Skills.Tests/McpToolDescriptorTests.cs` (descriptor-level, no transport)
- Modify: `tests/TaxClaw.Skills.Tests` reference to add the MCP project

- [ ] **Step 1: Add references for the MCP descriptor test**

```bash
dotnet add tests/TaxClaw.Skills.Tests reference src/TaxClaw.Mcp src/TaxClaw.Agent
```

- [ ] **Step 2: Write the failing test**

Create `tests/TaxClaw.Skills.Tests/McpToolDescriptorTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Mcp;
using Xunit;

namespace TaxClaw.Skills.Tests;

public class McpToolDescriptorTests
{
    [Fact]
    public void Server_publishes_the_math_tools_for_mcp_clients()
    {
        IList<AITool> tools = TaxClawMcpServer.PublishedTools();
        var names = tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();

        Assert.Contains("add", names);
        Assert.Contains("round_to_unit", names);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: FAIL — `TaxClawMcpServer` does not exist.

- [ ] **Step 4: Write the minimal implementation**

Create `src/TaxClaw.Mcp/TaxClawMcpServer.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Agent;

namespace TaxClaw.Mcp;

/// <summary>
/// Bridges tax-claw's internal tools to the Model Context Protocol so the same capabilities can be
/// consumed by, or shared with, other MCP-aware agents. <see cref="PublishedTools"/> is the single
/// list of tools exposed; transport wiring (stdio/HTTP via the MCP SDK) builds on top of it.
/// </summary>
public static class TaxClawMcpServer
{
    /// <summary>The internal tools made available over MCP. Extend as new tool groups are added.</summary>
    public static IList<AITool> PublishedTools() => MathTools.CreateTools();
}
```

> The MCP SDK transport host (`ModelContextProtocol` stdio/HTTP server registering `PublishedTools()`)
> is wired in the TUI composition root when the app gains a `--serve-mcp` mode. This task establishes
> and tests the published-tool surface, which is the part with logic worth asserting.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Skills.Tests`
Expected: PASS.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 7: Commit**

```bash
git add src/TaxClaw.Mcp/TaxClawMcpServer.cs tests/TaxClaw.Skills.Tests/McpToolDescriptorTests.cs
git commit -m "feat(mcp): publish internal tools over MCP surface"
```

---

## Self-Review

**1. Spec coverage:**
- Skill = knowledge pack with manifest (id, version, pinned law/form, author, hash) → Task 2. ✓
- Load installed skills, reject tampered → Task 4. ✓
- Shareable vs private split + PII scanner before export → Tasks 3, 5. ✓
- Export pack (no PII by construction + scanner) → Task 5. ✓
- Import = untrusted; staged pending approval, never auto-activated; hash + PII gates → Task 6. ✓
- Approval/sandbox/tests reuse Plan 2 `ApprovalGate`/`ScriptCompiler`; version pin via manifest + Plan 5 `ArtifactInvalidator` → Tasks 2, 6 (referenced). ✓
- MCP for tools; no separate plugin mechanism → Task 7. ✓
- Git-repo channel: packs are plain files committed via PR (no in-app registry) → Tasks 5, 6. ✓

**2. Placeholder scan:** No TBD/TODO. Every code step complete; tests assert real behavior. The Task 7 note about transport hosting is an explicit scoped deferral — the published-tool surface (the logic) is implemented and tested. ✓

**3. Type consistency:** `SkillManifest(Id, Version, LawVersion, FormVersion, Author, ContentHash)` + `ComputeContentHash(files)` consistent (2, 4, 5, 6). `Skill(Manifest, Files)` consistent (4). `ExportedPack(Manifest, Files)` consistent (5, 6). `PiiScanner.Scan(files) -> IReadOnlyList<PiiFinding>` consistent (3, 5, 6). `ImportResult(Outcome, StagedPath)` + `ImportOutcome` consistent (6). `TaxClawMcpServer.PublishedTools()` returns `IList<AITool>` matching `MathTools.CreateTools()` from Plan 1 (7). ✓
