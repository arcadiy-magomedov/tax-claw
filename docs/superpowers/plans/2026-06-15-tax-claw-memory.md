# tax-claw Memory & Feedback — Implementation Plan (Plan 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the agent durable memory — scoped facts, user corrections that take priority over defaults, and learned artifacts that are invalidated when the law/form version they were pinned to changes — and inject the relevant memory into each turn so the assistant gets more accurate over time.

**Architecture:** A new `TaxClaw.Memory` library. `MemoryEntry` records carry a `MemoryScope` (global/project/doc-type) and a kind (fact/feedback/preference/artifact). `JsonMemoryStore` persists them under the data root. `Feedback` corrections are ranked above defaults by a `MemoryContextProvider` that renders the most relevant entries into a system-context string the agent prepends. `VersionedArtifact`s (learned parsers/calc functions) are pinned to a law/form version; `ArtifactInvalidator` drops those whose version no longer matches the active one.

**Tech Stack:** .NET 10, `System.Text.Json`, xUnit. Builds on Plan 1 (`TaxClaw.Core`, `StorageRoot`) and the agent seam; complements Plans 2 & 4 (pinned functions/parsers).

---

## File Structure

- `src/TaxClaw.Memory/MemoryScope.cs` — scope value object (global / project / doc-type).
- `src/TaxClaw.Memory/MemoryEntry.cs` — entry + kinds (`Fact`, `Feedback`, `Preference`).
- `src/TaxClaw.Memory/IMemoryStore.cs` — query/add abstraction.
- `src/TaxClaw.Memory/JsonMemoryStore.cs` — JSON-file persistence under the data root.
- `src/TaxClaw.Memory/MemoryContextProvider.cs` — select + render memory for a turn.
- `src/TaxClaw.Memory/VersionedArtifact.cs`, `ArtifactInvalidator.cs` — version-pinned learned artifacts.
- Tests under `tests/TaxClaw.Memory.Tests/`.

---

### Task 1: Scaffold the memory library

**Files:**
- Create: `src/TaxClaw.Memory`, `tests/TaxClaw.Memory.Tests`

- [ ] **Step 1: Create and reference projects**

```bash
dotnet new classlib -o src/TaxClaw.Memory
dotnet new xunit    -o tests/TaxClaw.Memory.Tests
rm src/TaxClaw.Memory/Class1.cs tests/TaxClaw.Memory.Tests/UnitTest1.cs

dotnet sln add src/TaxClaw.Memory tests/TaxClaw.Memory.Tests
dotnet add src/TaxClaw.Memory reference src/TaxClaw.Core src/TaxClaw.Storage
dotnet add tests/TaxClaw.Memory.Tests reference src/TaxClaw.Core src/TaxClaw.Storage src/TaxClaw.Memory
```

- [ ] **Step 2: Verify build, then commit**

Run: `dotnet build`
Expected: `Build succeeded.`

```bash
git add -A
git commit -m "chore(memory): scaffold memory library"
```

---

### Task 2: Memory scope

**Files:**
- Create: `src/TaxClaw.Memory/MemoryScope.cs`
- Test: `tests/TaxClaw.Memory.Tests/MemoryScopeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Memory.Tests/MemoryScopeTests.cs`:

```csharp
using TaxClaw.Memory;
using Xunit;

namespace TaxClaw.Memory.Tests;

public class MemoryScopeTests
{
    [Fact]
    public void Global_scope_is_relevant_to_every_context()
    {
        var scope = MemoryScope.Global();
        Assert.True(scope.IsRelevantTo(projectId: "2027", documentType: "dividend"));
        Assert.True(scope.IsRelevantTo(projectId: null, documentType: null));
    }

    [Fact]
    public void Project_scope_matches_only_its_project()
    {
        var scope = MemoryScope.Project("2027");
        Assert.True(scope.IsRelevantTo("2027", null));
        Assert.False(scope.IsRelevantTo("2026", null));
    }

    [Fact]
    public void DocumentType_scope_matches_only_its_type()
    {
        var scope = MemoryScope.DocumentType("dividend");
        Assert.True(scope.IsRelevantTo("2027", "dividend"));
        Assert.False(scope.IsRelevantTo("2027", "rsu_vesting"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: FAIL — `MemoryScope` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Memory/MemoryScope.cs`:

```csharp
namespace TaxClaw.Memory;

public enum ScopeKind { Global, Project, DocumentType }

/// <summary>
/// Where a memory applies. Global memory is always in play; project- and doc-type-scoped memory
/// only surfaces in matching contexts. This is how "re-confirm per project" and targeted feedback
/// are expressed.
/// </summary>
public sealed record MemoryScope(ScopeKind Kind, string? Key)
{
    public static MemoryScope Global() => new(ScopeKind.Global, null);
    public static MemoryScope Project(string projectId) => new(ScopeKind.Project, projectId);
    public static MemoryScope DocumentType(string documentType) => new(ScopeKind.DocumentType, documentType);

    public bool IsRelevantTo(string? projectId, string? documentType) => Kind switch
    {
        ScopeKind.Global => true,
        ScopeKind.Project => Key == projectId,
        ScopeKind.DocumentType => Key == documentType,
        _ => false
    };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Memory/MemoryScope.cs tests/TaxClaw.Memory.Tests/MemoryScopeTests.cs
git commit -m "feat(memory): add memory scope"
```

---

### Task 3: Memory entry

**Files:**
- Create: `src/TaxClaw.Memory/MemoryEntry.cs`
- Test: `tests/TaxClaw.Memory.Tests/MemoryEntryTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Memory.Tests/MemoryEntryTests.cs`:

```csharp
using TaxClaw.Memory;
using Xunit;

namespace TaxClaw.Memory.Tests;

public class MemoryEntryTests
{
    [Fact]
    public void Feedback_outranks_fact_which_outranks_preference()
    {
        Assert.True(MemoryKind.Feedback.Priority() > MemoryKind.Fact.Priority());
        Assert.True(MemoryKind.Fact.Priority() > MemoryKind.Preference.Priority());
    }

    [Fact]
    public void Entry_keeps_its_scope_kind_and_text()
    {
        var entry = new MemoryEntry(
            Id: "m1",
            Kind: MemoryKind.Feedback,
            Scope: MemoryScope.DocumentType("rsu_vesting"),
            Text: "Treat Microsoft RSUs as § 6 employment income.",
            CreatedAt: DateTimeOffset.UnixEpoch);

        Assert.Equal(MemoryKind.Feedback, entry.Kind);
        Assert.True(entry.Scope.IsRelevantTo("2027", "rsu_vesting"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: FAIL — `MemoryEntry` / `MemoryKind` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Memory/MemoryEntry.cs`:

```csharp
namespace TaxClaw.Memory;

/// <summary>The kind of remembered item. Priority orders how strongly it overrides defaults.</summary>
public enum MemoryKind { Preference, Fact, Feedback }

public static class MemoryKindExtensions
{
    /// <summary>Higher wins: user feedback/corrections outrank facts, which outrank preferences.</summary>
    public static int Priority(this MemoryKind kind) => kind switch
    {
        MemoryKind.Feedback => 3,
        MemoryKind.Fact => 2,
        MemoryKind.Preference => 1,
        _ => 0
    };
}

/// <summary>A single remembered item with its scope and provenance timestamp.</summary>
public sealed record MemoryEntry(
    string Id,
    MemoryKind Kind,
    MemoryScope Scope,
    string Text,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Memory/MemoryEntry.cs tests/TaxClaw.Memory.Tests/MemoryEntryTests.cs
git commit -m "feat(memory): add memory entry and priority ordering"
```

---

### Task 4: JSON memory store

**Files:**
- Create: `src/TaxClaw.Memory/IMemoryStore.cs`
- Create: `src/TaxClaw.Memory/JsonMemoryStore.cs`
- Test: `tests/TaxClaw.Memory.Tests/JsonMemoryStoreTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Memory.Tests/JsonMemoryStoreTests.cs`:

```csharp
using TaxClaw.Memory;
using TaxClaw.Storage;
using Xunit;

namespace TaxClaw.Memory.Tests;

public class JsonMemoryStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-mem-" + Guid.NewGuid().ToString("N"));

    private JsonMemoryStore NewStore() => new(new StorageRoot(_root));

    [Fact]
    public async Task Added_entries_persist_and_reload()
    {
        var store = NewStore();
        await store.AddAsync(new MemoryEntry("m1", MemoryKind.Feedback,
            MemoryScope.Global(), "Prefer Czech replies.", DateTimeOffset.UnixEpoch));

        var reloaded = NewStore();
        var all = await reloaded.QueryAsync(projectId: null, documentType: null);

        Assert.Single(all);
        Assert.Equal("Prefer Czech replies.", all[0].Text);
    }

    [Fact]
    public async Task Query_filters_by_relevance_to_context()
    {
        var store = NewStore();
        await store.AddAsync(new MemoryEntry("g", MemoryKind.Fact, MemoryScope.Global(), "g", DateTimeOffset.UnixEpoch));
        await store.AddAsync(new MemoryEntry("p27", MemoryKind.Fact, MemoryScope.Project("2027"), "p27", DateTimeOffset.UnixEpoch));
        await store.AddAsync(new MemoryEntry("p26", MemoryKind.Fact, MemoryScope.Project("2026"), "p26", DateTimeOffset.UnixEpoch));

        var hits = await store.QueryAsync(projectId: "2027", documentType: null);

        var ids = hits.Select(h => h.Id).ToHashSet();
        Assert.Contains("g", ids);
        Assert.Contains("p27", ids);
        Assert.DoesNotContain("p26", ids);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: FAIL — store types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Memory/IMemoryStore.cs`:

```csharp
namespace TaxClaw.Memory;

public interface IMemoryStore
{
    Task AddAsync(MemoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryEntry>> QueryAsync(string? projectId, string? documentType, CancellationToken ct = default);
}
```

Create `src/TaxClaw.Memory/JsonMemoryStore.cs`:

```csharp
using System.Text.Json;
using TaxClaw.Storage;

namespace TaxClaw.Memory;

/// <summary>
/// Persists memory as a single JSON array under the data root. Queries return entries whose scope
/// is relevant to the given context, ordered by priority then recency.
/// </summary>
public sealed class JsonMemoryStore(StorageRoot root) : IMemoryStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private string File => Path.Combine(root.Path, "memory", "entries.json");

    public async Task AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var all = (await LoadAllAsync(ct)).ToList();
        all.Add(entry);

        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        await using var stream = System.IO.File.Create(File);
        await JsonSerializer.SerializeAsync(stream, all, Json, ct);
    }

    public async Task<IReadOnlyList<MemoryEntry>> QueryAsync(string? projectId, string? documentType, CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return all
            .Where(e => e.Scope.IsRelevantTo(projectId, documentType))
            .OrderByDescending(e => e.Kind.Priority())
            .ThenByDescending(e => e.CreatedAt)
            .ToList();
    }

    private async Task<IReadOnlyList<MemoryEntry>> LoadAllAsync(CancellationToken ct)
    {
        if (!System.IO.File.Exists(File))
        {
            return [];
        }

        await using var stream = System.IO.File.OpenRead(File);
        return await JsonSerializer.DeserializeAsync<List<MemoryEntry>>(stream, Json, ct) ?? [];
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS — persistence + relevance filtering both green.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Memory/IMemoryStore.cs src/TaxClaw.Memory/JsonMemoryStore.cs tests/TaxClaw.Memory.Tests/JsonMemoryStoreTests.cs
git commit -m "feat(memory): add JSON memory store with scoped queries"
```

---

### Task 5: Memory context provider

**Files:**
- Create: `src/TaxClaw.Memory/MemoryContextProvider.cs`
- Test: `tests/TaxClaw.Memory.Tests/MemoryContextProviderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Memory.Tests/MemoryContextProviderTests.cs`:

```csharp
using TaxClaw.Memory;
using TaxClaw.Storage;
using Xunit;

namespace TaxClaw.Memory.Tests;

public class MemoryContextProviderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-memctx-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Renders_relevant_memory_with_feedback_first()
    {
        var store = new JsonMemoryStore(new StorageRoot(_root));
        await store.AddAsync(new MemoryEntry("pref", MemoryKind.Preference, MemoryScope.Global(),
            "Reply in Czech.", DateTimeOffset.UnixEpoch));
        await store.AddAsync(new MemoryEntry("fb", MemoryKind.Feedback, MemoryScope.Global(),
            "Treat Microsoft RSUs as § 6.", DateTimeOffset.UnixEpoch.AddDays(1)));

        var provider = new MemoryContextProvider(store);
        string context = await provider.BuildContextAsync(projectId: "2027", documentType: null);

        Assert.Contains("§ 6", context);
        Assert.True(context.IndexOf("§ 6", StringComparison.Ordinal)
                    < context.IndexOf("Reply in Czech", StringComparison.Ordinal),
            "feedback should be rendered before lower-priority preferences");
    }

    [Fact]
    public async Task Empty_memory_yields_empty_context()
    {
        var provider = new MemoryContextProvider(new JsonMemoryStore(new StorageRoot(_root)));
        Assert.Equal(string.Empty, await provider.BuildContextAsync(null, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: FAIL — `MemoryContextProvider` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Memory/MemoryContextProvider.cs`:

```csharp
using System.Text;

namespace TaxClaw.Memory;

/// <summary>
/// Renders the memory relevant to the current context into a text block the agent prepends to its
/// system context. Feedback/corrections come first so they visibly override default behavior.
/// </summary>
public sealed class MemoryContextProvider(IMemoryStore store, int maxEntries = 20)
{
    public async Task<string> BuildContextAsync(string? projectId, string? documentType, CancellationToken ct = default)
    {
        var entries = await store.QueryAsync(projectId, documentType, ct);
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Remembered context (user corrections take priority over your defaults):");
        foreach (MemoryEntry e in entries.Take(maxEntries))
        {
            sb.AppendLine($"- [{e.Kind}] {e.Text}");
        }
        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS — feedback rendered before preference.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Memory/MemoryContextProvider.cs tests/TaxClaw.Memory.Tests/MemoryContextProviderTests.cs
git commit -m "feat(memory): render scoped memory as agent context"
```

---

### Task 6: Versioned artifacts and invalidation

**Files:**
- Create: `src/TaxClaw.Memory/VersionedArtifact.cs`
- Create: `src/TaxClaw.Memory/ArtifactInvalidator.cs`
- Test: `tests/TaxClaw.Memory.Tests/ArtifactInvalidatorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Memory.Tests/ArtifactInvalidatorTests.cs`:

```csharp
using TaxClaw.Memory;
using Xunit;

namespace TaxClaw.Memory.Tests;

public class ArtifactInvalidatorTests
{
    [Fact]
    public void Keeps_artifacts_pinned_to_the_active_version()
    {
        var artifacts = new[]
        {
            new VersionedArtifact("r38-fn", ArtifactKind.CalcFunction, lawVersion: "2027.1", formVersion: "25 5405/2027"),
            new VersionedArtifact("rsu-parser", ArtifactKind.DocumentParser, lawVersion: "2026.1", formVersion: "25 5405/2026")
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
            new VersionedArtifact("rsu-parser", ArtifactKind.DocumentParser, lawVersion: "2027.1", formVersion: null)
        };

        var valid = ArtifactInvalidator.SelectValid(artifacts, lawVersion: "2027.1", formVersion: "25 5405/2027");

        Assert.Single(valid);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: FAIL — artifact types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Memory/VersionedArtifact.cs`:

```csharp
namespace TaxClaw.Memory;

public enum ArtifactKind { CalcFunction, DocumentParser }

/// <summary>
/// A learned, reusable artifact pinned to the law (and, for calc functions, form) version it was
/// derived against. The pin is what lets us invalidate last year's rule when versions change.
/// </summary>
public sealed record VersionedArtifact(
    string Id,
    ArtifactKind Kind,
    string LawVersion,
    string? FormVersion);
```

Create `src/TaxClaw.Memory/ArtifactInvalidator.cs`:

```csharp
namespace TaxClaw.Memory;

/// <summary>
/// Selects artifacts still valid for the active versions. Calc functions must match both law and
/// form version; document parsers match on law version only (they do not depend on the form).
/// </summary>
public static class ArtifactInvalidator
{
    public static IReadOnlyList<VersionedArtifact> SelectValid(
        IEnumerable<VersionedArtifact> artifacts, string lawVersion, string formVersion)
    {
        return artifacts.Where(a => IsValid(a, lawVersion, formVersion)).ToList();
    }

    private static bool IsValid(VersionedArtifact artifact, string lawVersion, string formVersion)
    {
        if (artifact.LawVersion != lawVersion)
        {
            return false;
        }

        return artifact.Kind switch
        {
            ArtifactKind.CalcFunction => artifact.FormVersion == formVersion,
            ArtifactKind.DocumentParser => true,
            _ => false
        };
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS — last-year's parser dropped; matching calc function kept.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Memory/VersionedArtifact.cs src/TaxClaw.Memory/ArtifactInvalidator.cs tests/TaxClaw.Memory.Tests/ArtifactInvalidatorTests.cs
git commit -m "feat(memory): add version-pinned artifacts and invalidation"
```

---

### Task 7: Capture-feedback tool + inject memory into the agent

**Files:**
- Create: `src/TaxClaw.Memory/FeedbackTools.cs`
- Modify: `src/TaxClaw.Agent/TaxClawAgent.cs` (accept an optional context prefix per turn)
- Test: `tests/TaxClaw.Memory.Tests/FeedbackToolsTests.cs`
- Test: `tests/TaxClaw.Agent.Tests/AgentContextTests.cs`

- [ ] **Step 1: Add the AI package to the memory project**

```bash
dotnet add src/TaxClaw.Memory package Microsoft.Extensions.AI
```

- [ ] **Step 2: Write the failing test for the feedback tool**

Create `tests/TaxClaw.Memory.Tests/FeedbackToolsTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Memory;
using TaxClaw.Storage;
using Xunit;

namespace TaxClaw.Memory.Tests;

public class FeedbackToolsTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-fbtool-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Remember_feedback_persists_a_feedback_entry()
    {
        var store = new JsonMemoryStore(new StorageRoot(_root));
        var tools = new FeedbackTools(store, projectId: "2027");

        string ack = await tools.RememberFeedback("Treat Microsoft RSUs as § 6.", "rsu_vesting");

        Assert.Contains("remembered", ack, StringComparison.OrdinalIgnoreCase);
        var entries = await store.QueryAsync("2027", "rsu_vesting");
        Assert.Contains(entries, e => e.Kind == MemoryKind.Feedback && e.Text.Contains("§ 6"));
    }

    [Fact]
    public void CreateTools_exposes_remember_feedback()
    {
        var tools = new FeedbackTools(new JsonMemoryStore(new StorageRoot(_root)), "2027");
        var names = tools.CreateTools().OfType<AIFunction>().Select(f => f.Name).ToHashSet();
        Assert.Contains("remember_feedback", names);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: FAIL — `FeedbackTools` does not exist.

- [ ] **Step 4: Write the feedback tool**

Create `src/TaxClaw.Memory/FeedbackTools.cs`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace TaxClaw.Memory;

/// <summary>
/// Lets the agent persist a user correction as feedback memory. Optionally scoped to a document
/// type; otherwise scoped to the active project. Feedback outranks the agent's defaults later.
/// </summary>
public sealed class FeedbackTools(IMemoryStore store, string projectId)
{
    [Description("Remember a user correction so future runs honor it. Optionally scope it to a document type (e.g. 'rsu_vesting').")]
    public async Task<string> RememberFeedback(string correction, string? documentType = null)
    {
        MemoryScope scope = string.IsNullOrWhiteSpace(documentType)
            ? MemoryScope.Project(projectId)
            : MemoryScope.DocumentType(documentType);

        var entry = new MemoryEntry(
            Id: Guid.NewGuid().ToString("N"),
            Kind: MemoryKind.Feedback,
            Scope: scope,
            Text: correction,
            CreatedAt: DateTimeOffset.UtcNow);

        await store.AddAsync(entry);
        return "Got it — remembered.";
    }

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(RememberFeedback, name: "remember_feedback")
    ];
}
```

- [ ] **Step 5: Run the memory test to verify it passes**

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS.

- [ ] **Step 6: Write the failing agent-context test**

Create `tests/TaxClaw.Agent.Tests/AgentContextTests.cs`:

```csharp
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class AgentContextTests
{
    [Fact]
    public async Task Per_turn_context_is_sent_as_a_system_message()
    {
        var fake = new CapturingChatClient(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "ok")));
        var agent = new TaxClawAgent(fake, Prompts.System, MathTools.CreateTools());

        await agent.SendAsync("hi", turnContext: "Remembered: reply in Czech.");

        bool hasContext = fake.LastMessages!.Any(m =>
            m.Role == ChatRole.System && m.Text.Contains("reply in Czech"));
        Assert.True(hasContext);
    }

    private sealed class CapturingChatClient(ChatResponse response) : IChatClient
    {
        public IList<ChatMessage>? LastMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            foreach (var u in response.ToChatResponseUpdates()) yield return u;
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: FAIL — `SendAsync` has no `turnContext` parameter (compile error).

- [ ] **Step 8: Extend the agent with an optional per-turn context**

In `src/TaxClaw.Agent/TaxClawAgent.cs`, replace the `SendAsync` method:

```csharp
    public async Task<string> SendAsync(string userMessage, CancellationToken ct = default)
    {
        _history.Add(new ChatMessage(ChatRole.User, userMessage));
        ChatResponse response = await _client.GetResponseAsync(_history, _options, ct);
        _history.AddRange(response.Messages);
        return response.Text;
    }
```

with:

```csharp
    public async Task<string> SendAsync(
        string userMessage, string? turnContext = null, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>(_history);
        if (!string.IsNullOrWhiteSpace(turnContext))
        {
            messages.Add(new ChatMessage(ChatRole.System, turnContext));
        }
        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        ChatResponse response = await _client.GetResponseAsync(messages, _options, ct);

        // Persist only the durable turn (user input + model output), not the ephemeral context.
        _history.Add(new ChatMessage(ChatRole.User, userMessage));
        _history.AddRange(response.Messages);
        return response.Text;
    }
```

- [ ] **Step 8b: Update the TUI call site for the new signature**

The Plan 1 `AppHost` calls `agent.SendAsync(chat.Message, ct)` positionally. Now that the second
parameter is `string? turnContext`, that positional `CancellationToken` no longer binds — update the
call site to pass the token by name. In `src/TaxClaw.Tui/AppHost.cs`, replace:

```csharp
                case ChatCommand chat when chat.Message.Length > 0:
                    await AnsiConsole.Status().StartAsync("thinking…", async _ =>
                    {
                        string reply = await agent.SendAsync(chat.Message, ct);
                        AnsiConsole.MarkupLine($"[white]{Markup.Escape(reply)}[/]");
                    });
                    break;
```

with:

```csharp
                case ChatCommand chat when chat.Message.Length > 0:
                    await AnsiConsole.Status().StartAsync("thinking…", async _ =>
                    {
                        string reply = await agent.SendAsync(chat.Message, turnContext: null, ct);
                        AnsiConsole.MarkupLine($"[white]{Markup.Escape(reply)}[/]");
                    });
                    break;
```

> Passing `turnContext: null` keeps behavior identical for now. Wiring the actual memory context
> (building `turnContext` from `MemoryContextProvider` for the active project) is a composition step
> once `AppHost` carries the active project id; the agent seam and provider are fully tested here.

- [ ] **Step 9: Run both test projects to verify they pass**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: PASS — including the original `Send_returns_assistant_text_and_forwards_tools` (the parameter is optional, so existing calls still compile).

Run: `dotnet test tests/TaxClaw.Memory.Tests`
Expected: PASS.

- [ ] **Step 10: Run the full suite**

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat(memory): add feedback tool and per-turn memory injection into the agent"
```

---

## Self-Review

**1. Spec coverage:**
- Memory tiers (global profile-ish / project facts / doc-type) → Tasks 2, 3. ✓
- Feedback/corrections with priority over defaults → Tasks 3, 5, 7. ✓
- Persisted memory, reloads across runs → Task 4. ✓
- Re-inject relevant memory each turn (context provider + agent seam) → Tasks 5, 7. ✓
- Learned artifacts pinned to law/form version; invalidate on mismatch → Task 6. ✓
- Capture feedback from the conversation → Task 7. ✓
- *Note:* the cross-project `Profile` already exists (Plan 1); this plan adds the feedback/fact/preference tiers and the artifact-invalidation contract that Plans 2 & 4 registries consult when loading learned functions/parsers.

**2. Placeholder scan:** No TBD/TODO. Every code step complete; tests assert real behavior. ✓

**3. Type consistency:** `MemoryScope.Global/Project/DocumentType` + `IsRelevantTo(projectId, documentType)` consistent (2, 4, 5, 7). `MemoryEntry(Id, Kind, Scope, Text, CreatedAt)` consistent (3, 4, 5, 7). `MemoryKind` + `.Priority()` consistent (3, 4). `IMemoryStore.AddAsync/QueryAsync(projectId, documentType, ct)` consistent (4, 5, 7). `VersionedArtifact(Id, Kind, LawVersion, FormVersion)` + `ArtifactInvalidator.SelectValid(..., lawVersion, formVersion)` consistent (6). `TaxClawAgent.SendAsync(userMessage, turnContext?, ct)` — optional param keeps Plan 1/earlier call sites valid (7). ✓
