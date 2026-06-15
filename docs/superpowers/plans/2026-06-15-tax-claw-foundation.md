# tax-claw Foundation & Walking Skeleton — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a runnable .NET TUI application that creates per-year tax-declaration projects, persists a cross-project profile, and chats with a provider-agnostic LLM that calls deterministic decimal-math tools — the foundation every later subsystem plugs into.

**Architecture:** A thin, well-bounded .NET solution. Pure domain + decimal-math primitives in `TaxClaw.Core` (no external deps). LLM provider selection (`Azure OpenAI` / `OpenAI` / `Ollama`) behind an `IChatClientFactory` in `TaxClaw.Llm`. A thin agent loop with function-invocation and the "never do float math yourself" guardrail in `TaxClaw.Agent`. Local JSON storage in `TaxClaw.Storage`. A Spectre.Console host in `TaxClaw.Tui`. **Deliberate sequencing:** Plan 1 builds on the *stable* `Microsoft.Extensions.AI` substrate (the same layer Microsoft Agent Framework sits on) and keeps the agent abstraction thin; the richer MAF agent, memory, and MCP land in later plans. This de-risks the foundation against MAF's still-moving API while keeping the same `IChatClient` seam MAF uses.

**Tech Stack:** .NET 10, C# (latest), `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `Azure.AI.OpenAI`, `OllamaSharp`, `Spectre.Console`, `Microsoft.Extensions.Configuration`, xUnit.

---

## File Structure

**Source projects**
- `src/TaxClaw.Core/` — pure domain, no external deps.
  - `Math/DecimalMath.cs` — exact decimal arithmetic primitives + `RoundToUnit`.
  - `Math/CzechTaxRounding.cs` — CZ rounding conventions composed from `RoundToUnit`.
  - `Model/TaxYear.cs`, `Model/Profile.cs`, `Model/Project.cs` — core records.
  - `Storage/IProfileStore.cs`, `Storage/IProjectStore.cs` — storage abstractions.
- `src/TaxClaw.Llm/` — provider-agnostic LLM wiring.
  - `LlmOptions.cs` — bound from config.
  - `IChatClientFactory.cs`, `ChatClientFactory.cs` — builds an `IChatClient` per provider.
- `src/TaxClaw.Agent/` — app/agent logic (no console I/O).
  - `MathTools.cs` — `AIFunction` wrappers over `DecimalMath`.
  - `Prompts.cs` — system prompt with the no-float guardrail.
  - `TaxClawAgent.cs` — chat loop with function invocation.
  - `Commands/CommandRouter.cs`, `Commands/Commands.cs` — parse a TUI line into a command.
- `src/TaxClaw.Storage/` — JSON file persistence.
  - `JsonProfileStore.cs`, `JsonProjectStore.cs`, `StorageRoot.cs`.
- `src/TaxClaw.Tui/` — Spectre.Console executable.
  - `Program.cs`, `appsettings.json`, `AppHost.cs`.

**Test projects**
- `tests/TaxClaw.Core.Tests/` — math + model.
- `tests/TaxClaw.Storage.Tests/` — JSON store round-trips (temp dir).
- `tests/TaxClaw.Llm.Tests/` — factory selection + validation (no network).
- `tests/TaxClaw.Agent.Tests/` — tools, agent loop (fake `IChatClient`), command router.

**Build config**
- `Directory.Build.props` — shared `TargetFramework`, `Nullable`, `ImplicitUsings`, `LangVersion`.

---

### Task 1: Solution scaffold and build config

**Files:**
- Create: `Directory.Build.props`
- Create: `TaxClaw.sln` and all `src/*` + `tests/*` projects

- [ ] **Step 1: Create the solution and projects**

Run from the repo root (`/Users/magom001/Documents/projects/tax-claw`):

```bash
dotnet new sln -n TaxClaw

dotnet new classlib -o src/TaxClaw.Core
dotnet new classlib -o src/TaxClaw.Llm
dotnet new classlib -o src/TaxClaw.Agent
dotnet new classlib -o src/TaxClaw.Storage
dotnet new console  -o src/TaxClaw.Tui

dotnet new xunit -o tests/TaxClaw.Core.Tests
dotnet new xunit -o tests/TaxClaw.Storage.Tests
dotnet new xunit -o tests/TaxClaw.Llm.Tests
dotnet new xunit -o tests/TaxClaw.Agent.Tests

# Remove template placeholder files
rm src/TaxClaw.Core/Class1.cs src/TaxClaw.Llm/Class1.cs src/TaxClaw.Agent/Class1.cs src/TaxClaw.Storage/Class1.cs
rm tests/TaxClaw.Core.Tests/UnitTest1.cs tests/TaxClaw.Storage.Tests/UnitTest1.cs tests/TaxClaw.Llm.Tests/UnitTest1.cs tests/TaxClaw.Agent.Tests/UnitTest1.cs

# Add everything to the solution
dotnet sln add src/TaxClaw.Core src/TaxClaw.Llm src/TaxClaw.Agent src/TaxClaw.Storage src/TaxClaw.Tui
dotnet sln add tests/TaxClaw.Core.Tests tests/TaxClaw.Storage.Tests tests/TaxClaw.Llm.Tests tests/TaxClaw.Agent.Tests
```

- [ ] **Step 2: Wire project references**

```bash
dotnet add src/TaxClaw.Llm     reference src/TaxClaw.Core
dotnet add src/TaxClaw.Agent   reference src/TaxClaw.Core
dotnet add src/TaxClaw.Storage reference src/TaxClaw.Core
dotnet add src/TaxClaw.Tui     reference src/TaxClaw.Core src/TaxClaw.Llm src/TaxClaw.Agent src/TaxClaw.Storage

dotnet add tests/TaxClaw.Core.Tests    reference src/TaxClaw.Core
dotnet add tests/TaxClaw.Storage.Tests reference src/TaxClaw.Core src/TaxClaw.Storage
dotnet add tests/TaxClaw.Llm.Tests     reference src/TaxClaw.Llm
dotnet add tests/TaxClaw.Agent.Tests   reference src/TaxClaw.Core src/TaxClaw.Agent
```

- [ ] **Step 3: Create `Directory.Build.props`**

Create `Directory.Build.props` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

Now remove the now-duplicated `<TargetFramework>` lines from each generated `.csproj` so the shared value is the single source of truth:

```bash
# macOS/BSD sed
find src tests -name '*.csproj' -exec sed -i '' '/<TargetFramework>net[0-9.]*<\/TargetFramework>/d' {} +
```

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors (warnings about empty projects are fine).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: scaffold TaxClaw solution and build config"
```

---

### Task 2: Decimal math primitives

**Files:**
- Create: `src/TaxClaw.Core/Math/DecimalMath.cs`
- Test: `tests/TaxClaw.Core.Tests/DecimalMathTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Core.Tests/DecimalMathTests.cs`:

```csharp
using TaxClaw.Core.Math;
using Xunit;

namespace TaxClaw.Core.Tests;

public class DecimalMathTests
{
    [Fact]
    public void Add_is_exact_for_decimals()
    {
        Assert.Equal(0.3m, DecimalMath.Add(0.1m, 0.2m));
    }

    [Fact]
    public void Subtract_and_multiply_are_exact()
    {
        Assert.Equal(0.2m, DecimalMath.Subtract(0.5m, 0.3m));
        Assert.Equal(6.25m, DecimalMath.Multiply(2.5m, 2.5m));
    }

    [Fact]
    public void Divide_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => DecimalMath.Divide(1m, 0m));
    }

    [Theory]
    [InlineData("12345", "100", "Down", "12300")]
    [InlineData("1234.01", "1", "Up", "1235")]
    [InlineData("1250", "100", "Nearest", "1300")]
    public void RoundToUnit_rounds_in_the_requested_direction(
        string value, string unit, string direction, string expected)
    {
        var result = DecimalMath.RoundToUnit(
            decimal.Parse(value),
            decimal.Parse(unit),
            Enum.Parse<RoundingDirection>(direction));

        Assert.Equal(decimal.Parse(expected), result);
    }

    [Fact]
    public void RoundToUnit_rejects_non_positive_unit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DecimalMath.RoundToUnit(100m, 0m, RoundingDirection.Down));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: FAIL — `DecimalMath` / `RoundingDirection` do not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Core/Math/DecimalMath.cs`:

```csharp
namespace TaxClaw.Core.Math;

/// <summary>Direction used when snapping a value to a rounding unit.</summary>
public enum RoundingDirection
{
    Up,
    Down,
    Nearest
}

/// <summary>
/// Deterministic, exact decimal arithmetic. This is the ONLY sanctioned way for the
/// application (and the agent) to produce numbers — never binary floating point.
/// </summary>
public static class DecimalMath
{
    public static decimal Add(decimal a, decimal b) => a + b;

    public static decimal Subtract(decimal a, decimal b) => a - b;

    public static decimal Multiply(decimal a, decimal b) => a * b;

    public static decimal Divide(decimal a, decimal b)
    {
        if (b == 0m)
        {
            throw new DivideByZeroException("Division by zero is not allowed.");
        }

        return a / b;
    }

    /// <summary>
    /// Snaps <paramref name="value"/> to the nearest multiple of <paramref name="unit"/>
    /// in the requested <paramref name="direction"/>. Pure arithmetic — tax-specific
    /// conventions (which unit, which direction) live in <see cref="CzechTaxRounding"/>.
    /// </summary>
    public static decimal RoundToUnit(decimal value, decimal unit, RoundingDirection direction)
    {
        if (unit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be positive.");
        }

        decimal quotient = value / unit;
        decimal rounded = direction switch
        {
            RoundingDirection.Up => decimal.Ceiling(quotient),
            RoundingDirection.Down => decimal.Floor(quotient),
            RoundingDirection.Nearest => decimal.Round(quotient, 0, MidpointRounding.AwayFromZero),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown rounding direction.")
        };

        return rounded * unit;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS — all `DecimalMathTests` green.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Core/Math/DecimalMath.cs tests/TaxClaw.Core.Tests/DecimalMathTests.cs
git commit -m "feat(core): add exact decimal math primitives"
```

---

### Task 3: Czech tax rounding helpers

**Files:**
- Create: `src/TaxClaw.Core/Math/CzechTaxRounding.cs`
- Test: `tests/TaxClaw.Core.Tests/CzechTaxRoundingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Core.Tests/CzechTaxRoundingTests.cs`:

```csharp
using TaxClaw.Core.Math;
using Xunit;

namespace TaxClaw.Core.Tests;

public class CzechTaxRoundingTests
{
    [Theory]
    [InlineData("156789", "156700")]
    [InlineData("156700", "156700")]
    [InlineData("99", "0")]
    public void Tax_base_is_rounded_down_to_whole_hundreds(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected),
            CzechTaxRounding.TaxBaseToHundredsDown(decimal.Parse(input)));
    }

    [Theory]
    [InlineData("1234.01", "1235")]
    [InlineData("1234.00", "1234")]
    [InlineData("0.01", "1")]
    public void Tax_is_rounded_up_to_whole_crowns(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected),
            CzechTaxRounding.TaxToWholeCrownsUp(decimal.Parse(input)));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: FAIL — `CzechTaxRounding` does not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Core/Math/CzechTaxRounding.cs`:

```csharp
namespace TaxClaw.Core.Math;

/// <summary>
/// Czech rounding conventions for tax figures, composed from <see cref="DecimalMath.RoundToUnit"/>.
/// The conventions encoded here (base → down to hundreds, tax → up to whole crowns) are the
/// standard <em>zaokrouhlení</em> rules; the authoritative thresholds are reconfirmed against
/// legislation in the calc-runtime plan, where these helpers are invoked by generated functions.
/// </summary>
public static class CzechTaxRounding
{
    /// <summary>Tax base (základ daně) rounded down to whole hundreds of CZK.</summary>
    public static decimal TaxBaseToHundredsDown(decimal taxBase) =>
        DecimalMath.RoundToUnit(taxBase, 100m, RoundingDirection.Down);

    /// <summary>Tax (daň) rounded up to whole crowns.</summary>
    public static decimal TaxToWholeCrownsUp(decimal tax) =>
        DecimalMath.RoundToUnit(tax, 1m, RoundingDirection.Up);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Core/Math/CzechTaxRounding.cs tests/TaxClaw.Core.Tests/CzechTaxRoundingTests.cs
git commit -m "feat(core): add Czech tax rounding helpers"
```

---

### Task 4: Domain models (TaxYear, Profile, Project)

**Files:**
- Create: `src/TaxClaw.Core/Model/TaxYear.cs`
- Create: `src/TaxClaw.Core/Model/Profile.cs`
- Create: `src/TaxClaw.Core/Model/Project.cs`
- Test: `tests/TaxClaw.Core.Tests/TaxYearTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Core.Tests/TaxYearTests.cs`:

```csharp
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Core.Tests;

public class TaxYearTests
{
    [Fact]
    public void Of_accepts_a_valid_year()
    {
        var year = TaxYear.Of(2027);
        Assert.Equal(2027, year.Year);
        Assert.Equal("2027", year.ToString());
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Of_rejects_out_of_range_years(int invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxYear.Of(invalid));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: FAIL — `TaxYear` does not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Core/Model/TaxYear.cs`:

```csharp
namespace TaxClaw.Core.Model;

/// <summary>A Czech tax year that a declaration project targets.</summary>
public sealed record TaxYear(int Year)
{
    public static TaxYear Of(int year) =>
        year is >= 2000 and <= 2100
            ? new TaxYear(year)
            : throw new ArgumentOutOfRangeException(nameof(year), year, "Tax year must be between 2000 and 2100.");

    public override string ToString() => Year.ToString();
}
```

Create `src/TaxClaw.Core/Model/Profile.cs`:

```csharp
namespace TaxClaw.Core.Model;

/// <summary>
/// Cross-project personal information. Captured once and re-confirmed (and snapshotted)
/// into every new project. Treated as sensitive (PII) by later plans.
/// </summary>
public sealed record Profile
{
    public required string FullName { get; init; }
    public string? RodneCislo { get; init; }
    public string? Address { get; init; }
    public string? Employer { get; init; }
    public string? BankAccount { get; init; }
}
```

Create `src/TaxClaw.Core/Model/Project.cs`:

```csharp
namespace TaxClaw.Core.Model;

public enum ProjectStatus
{
    Draft,
    CollectingDocuments,
    Calculated,
    Reviewed,
    Exported,
    Filed
}

/// <summary>A single tax declaration for one tax year (e.g. "Declaration 2027").</summary>
public sealed record Project
{
    public required string Id { get; init; }
    public required TaxYear Year { get; init; }
    public required Profile ProfileSnapshot { get; init; }
    public ProjectStatus Status { get; init; } = ProjectStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Core/Model tests/TaxClaw.Core.Tests/TaxYearTests.cs
git commit -m "feat(core): add TaxYear, Profile, and Project models"
```

---

### Task 5: Storage abstractions + JSON file stores

**Files:**
- Create: `src/TaxClaw.Core/Storage/IProfileStore.cs`
- Create: `src/TaxClaw.Core/Storage/IProjectStore.cs`
- Create: `src/TaxClaw.Storage/StorageRoot.cs`
- Create: `src/TaxClaw.Storage/JsonProfileStore.cs`
- Create: `src/TaxClaw.Storage/JsonProjectStore.cs`
- Test: `tests/TaxClaw.Storage.Tests/JsonStoreTests.cs`

- [ ] **Step 1: Write the storage abstractions (no test yet — interfaces)**

Create `src/TaxClaw.Core/Storage/IProfileStore.cs`:

```csharp
using TaxClaw.Core.Model;

namespace TaxClaw.Core.Storage;

public interface IProfileStore
{
    Task<Profile?> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(Profile profile, CancellationToken ct = default);
}
```

Create `src/TaxClaw.Core/Storage/IProjectStore.cs`:

```csharp
using TaxClaw.Core.Model;

namespace TaxClaw.Core.Storage;

public interface IProjectStore
{
    Task<Project> CreateAsync(TaxYear year, Profile profileSnapshot, CancellationToken ct = default);
    Task<Project?> LoadAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing test**

Create `tests/TaxClaw.Storage.Tests/JsonStoreTests.cs`:

```csharp
using TaxClaw.Core.Model;
using TaxClaw.Storage;
using Xunit;

namespace TaxClaw.Storage.Tests;

public class JsonStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-tests-" + Guid.NewGuid().ToString("N"));

    private StorageRoot Root => new(_root);

    [Fact]
    public async Task Profile_round_trips_through_disk()
    {
        var store = new JsonProfileStore(Root);
        var profile = new Profile { FullName = "Jan Novák", RodneCislo = "900101/1234" };

        await store.SaveAsync(profile);
        var loaded = await store.LoadAsync();

        Assert.Equal(profile, loaded);
    }

    [Fact]
    public async Task Loading_a_missing_profile_returns_null()
    {
        var store = new JsonProfileStore(Root);
        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Create_then_load_and_list_a_project()
    {
        var store = new JsonProjectStore(Root);
        var profile = new Profile { FullName = "Jan Novák" };

        var created = await store.CreateAsync(TaxYear.Of(2027), profile);

        Assert.Equal("2027", created.Id);
        Assert.Equal(ProjectStatus.Draft, created.Status);

        var loaded = await store.LoadAsync("2027");
        Assert.Equal(created, loaded);

        var ids = await store.ListAsync();
        Assert.Contains("2027", ids);
    }

    [Fact]
    public async Task Creating_a_duplicate_project_throws()
    {
        var store = new JsonProjectStore(Root);
        var profile = new Profile { FullName = "Jan Novák" };

        await store.CreateAsync(TaxYear.Of(2027), profile);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateAsync(TaxYear.Of(2027), profile));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Storage.Tests`
Expected: FAIL — `StorageRoot` / `JsonProfileStore` / `JsonProjectStore` do not exist (compile error).

- [ ] **Step 4: Write the minimal implementation**

Create `src/TaxClaw.Storage/StorageRoot.cs`:

```csharp
namespace TaxClaw.Storage;

/// <summary>
/// Resolves on-disk locations under the tax-claw data root. Defaults to <c>~/.tax-claw</c>;
/// tests inject a temp directory.
/// </summary>
public sealed class StorageRoot
{
    public StorageRoot(string? rootPath = null)
    {
        Path = rootPath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tax-claw");
    }

    public string Path { get; }

    public string ProfileFile => System.IO.Path.Combine(Path, "profile.json");

    public string ProjectsDirectory => System.IO.Path.Combine(Path, "projects");

    public string ProjectDirectory(string id) => System.IO.Path.Combine(ProjectsDirectory, id);

    public string ProjectFile(string id) => System.IO.Path.Combine(ProjectDirectory(id), "project.json");
}
```

Create `src/TaxClaw.Storage/JsonProfileStore.cs`:

```csharp
using System.Text.Json;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;

namespace TaxClaw.Storage;

public sealed class JsonProfileStore(StorageRoot root) : IProfileStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<Profile?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(root.ProfileFile))
        {
            return null;
        }

        await using var stream = File.OpenRead(root.ProfileFile);
        return await JsonSerializer.DeserializeAsync<Profile>(stream, Json, ct);
    }

    public async Task SaveAsync(Profile profile, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root.Path);
        await using var stream = File.Create(root.ProfileFile);
        await JsonSerializer.SerializeAsync(stream, profile, Json, ct);
    }
}
```

Create `src/TaxClaw.Storage/JsonProjectStore.cs`:

```csharp
using System.Text.Json;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;

namespace TaxClaw.Storage;

public sealed class JsonProjectStore(StorageRoot root) : IProjectStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<Project> CreateAsync(TaxYear year, Profile profileSnapshot, CancellationToken ct = default)
    {
        string id = year.ToString();
        string dir = root.ProjectDirectory(id);

        if (Directory.Exists(dir))
        {
            throw new InvalidOperationException($"Project '{id}' already exists.");
        }

        Directory.CreateDirectory(dir);

        var project = new Project
        {
            Id = id,
            Year = year,
            ProfileSnapshot = profileSnapshot,
            Status = ProjectStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var stream = File.Create(root.ProjectFile(id));
        await JsonSerializer.SerializeAsync(stream, project, Json, ct);
        return project;
    }

    public async Task<Project?> LoadAsync(string id, CancellationToken ct = default)
    {
        string file = root.ProjectFile(id);
        if (!File.Exists(file))
        {
            return null;
        }

        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<Project>(stream, Json, ct);
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(root.ProjectsDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> ids = Directory
            .GetDirectories(root.ProjectsDirectory)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();

        return Task.FromResult(ids);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Storage.Tests`
Expected: PASS — all four `JsonStoreTests` green.

- [ ] **Step 6: Commit**

```bash
git add src/TaxClaw.Core/Storage src/TaxClaw.Storage tests/TaxClaw.Storage.Tests/JsonStoreTests.cs
git commit -m "feat(storage): add JSON profile and project stores"
```

---

### Task 6: LLM options + provider-agnostic ChatClientFactory

**Files:**
- Add packages to `src/TaxClaw.Llm`
- Create: `src/TaxClaw.Llm/LlmOptions.cs`
- Create: `src/TaxClaw.Llm/IChatClientFactory.cs`
- Create: `src/TaxClaw.Llm/ChatClientFactory.cs`
- Test: `tests/TaxClaw.Llm.Tests/ChatClientFactoryTests.cs`

- [ ] **Step 1: Add the provider packages**

```bash
dotnet add src/TaxClaw.Llm package Microsoft.Extensions.AI
dotnet add src/TaxClaw.Llm package Microsoft.Extensions.AI.OpenAI --prerelease
dotnet add src/TaxClaw.Llm package Azure.AI.OpenAI
dotnet add src/TaxClaw.Llm package OllamaSharp
```

- [ ] **Step 2: Write the failing test**

Create `tests/TaxClaw.Llm.Tests/ChatClientFactoryTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Llm;
using Xunit;

namespace TaxClaw.Llm.Tests;

public class ChatClientFactoryTests
{
    [Fact]
    public void Ollama_provider_builds_a_chat_client_without_network()
    {
        var options = new LlmOptions { Provider = "ollama", Model = "llama3.1" };
        IChatClient client = new ChatClientFactory(options).Create();
        Assert.NotNull(client);
    }

    [Fact]
    public void Unknown_provider_throws()
    {
        var options = new LlmOptions { Provider = "does-not-exist", Model = "x" };
        Assert.Throws<NotSupportedException>(() => new ChatClientFactory(options).Create());
    }

    [Fact]
    public void Azure_provider_requires_an_endpoint()
    {
        var options = new LlmOptions { Provider = "azure", Model = "gpt-4o", ApiKey = "k" };
        Assert.Throws<ArgumentException>(() => new ChatClientFactory(options).Create());
    }

    [Fact]
    public void OpenAi_provider_requires_an_api_key()
    {
        var options = new LlmOptions { Provider = "openai", Model = "gpt-4o" };
        Assert.Throws<ArgumentException>(() => new ChatClientFactory(options).Create());
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Llm.Tests`
Expected: FAIL — `LlmOptions` / `ChatClientFactory` do not exist (compile error).

- [ ] **Step 4: Write the minimal implementation**

Create `src/TaxClaw.Llm/LlmOptions.cs`:

```csharp
namespace TaxClaw.Llm;

/// <summary>Provider-agnostic LLM configuration, bound from the "Llm" config section.</summary>
public sealed class LlmOptions
{
    /// <summary>One of: "ollama", "openai", "azure".</summary>
    public string Provider { get; set; } = "ollama";

    public string Model { get; set; } = "llama3.1";

    /// <summary>Required for "azure"; optional override for "ollama" (defaults to localhost).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Required for "openai" and "azure".</summary>
    public string? ApiKey { get; set; }
}
```

Create `src/TaxClaw.Llm/IChatClientFactory.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

public interface IChatClientFactory
{
    IChatClient Create();
}
```

Create `src/TaxClaw.Llm/ChatClientFactory.cs`:

```csharp
using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace TaxClaw.Llm;

/// <summary>
/// Builds an <see cref="IChatClient"/> for the configured provider. This is the single seam
/// the rest of the app depends on, keeping every provider behind one stable abstraction.
/// </summary>
/// <remarks>
/// Note: on older <c>Microsoft.Extensions.AI.OpenAI</c> versions the bridge extension is named
/// <c>AsChatClient()</c> instead of <c>AsIChatClient()</c>.
/// </remarks>
public sealed class ChatClientFactory(LlmOptions options) : IChatClientFactory
{
    public IChatClient Create() => options.Provider.ToLowerInvariant() switch
    {
        "ollama" => new OllamaApiClient(
            new Uri(options.Endpoint ?? "http://localhost:11434"),
            options.Model),

        "openai" => new OpenAIClient(RequireApiKey())
            .GetChatClient(options.Model)
            .AsIChatClient(),

        "azure" => new AzureOpenAIClient(
                new Uri(RequireEndpoint()),
                new ApiKeyCredential(RequireApiKey()))
            .GetChatClient(options.Model)
            .AsIChatClient(),

        _ => throw new NotSupportedException($"Unknown LLM provider '{options.Provider}'.")
    };

    private string RequireApiKey() =>
        string.IsNullOrWhiteSpace(options.ApiKey)
            ? throw new ArgumentException($"Provider '{options.Provider}' requires an API key.")
            : options.ApiKey;

    private string RequireEndpoint() =>
        string.IsNullOrWhiteSpace(options.Endpoint)
            ? throw new ArgumentException($"Provider '{options.Provider}' requires an endpoint.")
            : options.Endpoint;
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Llm.Tests`
Expected: PASS — all four `ChatClientFactoryTests` green.

> If `AsIChatClient()` fails to resolve, the installed package version uses `AsChatClient()`; rename the two call sites and re-run.

- [ ] **Step 6: Commit**

```bash
git add src/TaxClaw.Llm tests/TaxClaw.Llm.Tests/ChatClientFactoryTests.cs
git commit -m "feat(llm): add provider-agnostic chat client factory"
```

---

### Task 7: Decimal math AI tools

**Files:**
- Add package to `src/TaxClaw.Agent`
- Create: `src/TaxClaw.Agent/MathTools.cs`
- Test: `tests/TaxClaw.Agent.Tests/MathToolsTests.cs`

- [ ] **Step 1: Add the Microsoft.Extensions.AI package**

```bash
dotnet add src/TaxClaw.Agent package Microsoft.Extensions.AI
```

- [ ] **Step 2: Write the failing test**

Create `tests/TaxClaw.Agent.Tests/MathToolsTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class MathToolsTests
{
    [Fact]
    public void Add_uses_exact_decimal_arithmetic()
    {
        Assert.Equal(0.3m, MathTools.Add(0.1m, 0.2m));
    }

    [Fact]
    public void CreateTools_exposes_the_expected_named_functions()
    {
        var names = MathTools.CreateTools()
            .OfType<AIFunction>()
            .Select(f => f.Name)
            .ToHashSet();

        Assert.Superset(
            new HashSet<string> { "add", "subtract", "multiply", "divide", "round_to_unit" },
            names);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: FAIL — `MathTools` does not exist (compile error).

- [ ] **Step 4: Write the minimal implementation**

Create `src/TaxClaw.Agent/MathTools.cs`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;
using TaxClaw.Core.Math;

namespace TaxClaw.Agent;

/// <summary>
/// The only sanctioned way for the agent to produce a number on the fly. Each tool is a thin,
/// deterministic wrapper over <see cref="DecimalMath"/> — the model must call these instead of
/// doing arithmetic itself.
/// </summary>
public static class MathTools
{
    [Description("Add two exact decimal numbers and return their sum.")]
    public static decimal Add(decimal a, decimal b) => DecimalMath.Add(a, b);

    [Description("Subtract b from a using exact decimal arithmetic.")]
    public static decimal Subtract(decimal a, decimal b) => DecimalMath.Subtract(a, b);

    [Description("Multiply two exact decimal numbers.")]
    public static decimal Multiply(decimal a, decimal b) => DecimalMath.Multiply(a, b);

    [Description("Divide a by b using exact decimal arithmetic. Errors if b is zero.")]
    public static decimal Divide(decimal a, decimal b) => DecimalMath.Divide(a, b);

    [Description("Round a value to the nearest multiple of unit. direction is 'Up', 'Down', or 'Nearest'.")]
    public static decimal RoundToUnit(decimal value, decimal unit, RoundingDirection direction) =>
        DecimalMath.RoundToUnit(value, unit, direction);

    /// <summary>Builds the tool list passed to the agent's chat options.</summary>
    public static IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(Add, name: "add"),
        AIFunctionFactory.Create(Subtract, name: "subtract"),
        AIFunctionFactory.Create(Multiply, name: "multiply"),
        AIFunctionFactory.Create(Divide, name: "divide"),
        AIFunctionFactory.Create(RoundToUnit, name: "round_to_unit")
    ];
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/TaxClaw.Agent/MathTools.cs tests/TaxClaw.Agent.Tests/MathToolsTests.cs
git commit -m "feat(agent): expose decimal math as AI tools"
```

---

### Task 8: Agent chat loop with function invocation

**Files:**
- Create: `src/TaxClaw.Agent/Prompts.cs`
- Create: `src/TaxClaw.Agent/TaxClawAgent.cs`
- Test: `tests/TaxClaw.Agent.Tests/TaxClawAgentTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Agent.Tests/TaxClawAgentTests.cs`:

```csharp
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class TaxClawAgentTests
{
    [Fact]
    public async Task Send_returns_assistant_text_and_forwards_tools()
    {
        var fake = new FakeChatClient(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "Hello, I can help with your declaration.")));

        var agent = new TaxClawAgent(fake, Prompts.System, MathTools.CreateTools());

        string reply = await agent.SendAsync("hi");

        Assert.Equal("Hello, I can help with your declaration.", reply);
        Assert.NotNull(fake.LastOptions);
        var toolNames = fake.LastOptions!.Tools!.OfType<AIFunction>().Select(t => t.Name);
        Assert.Contains("add", toolNames);
    }

    [Fact]
    public void System_prompt_states_the_no_float_guardrail()
    {
        Assert.Contains("never", Prompts.System, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool", Prompts.System, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeChatClient(ChatResponse response) : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: FAIL — `Prompts` / `TaxClawAgent` do not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Agent/Prompts.cs`:

```csharp
namespace TaxClaw.Agent;

public static class Prompts
{
    public const string System =
        """
        You are tax-claw, an assistant that helps prepare a Czech personal income tax
        declaration (Přiznání k dani z příjmů fyzických osob, form 25 5405).

        Hard rules:
        - You never perform arithmetic yourself. To add, subtract, multiply, divide, or round
          any number, you MUST call the provided math tools. Never compute with floating point.
        - You are a helper, not a tax adviser. Surface uncertainty and ask the user to confirm
          anything ambiguous rather than guessing.
        - Keep answers brief and concrete.
        """;
}
```

Create `src/TaxClaw.Agent/TaxClawAgent.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace TaxClaw.Agent;

/// <summary>
/// A thin, multi-turn chat agent over <see cref="IChatClient"/> with automatic function
/// invocation. Kept intentionally small; the richer Microsoft Agent Framework agent (memory,
/// MCP, middleware) replaces this in a later plan via the same <see cref="IChatClient"/> seam.
/// </summary>
public sealed class TaxClawAgent
{
    private readonly IChatClient _client;
    private readonly List<ChatMessage> _history;
    private readonly ChatOptions _options;

    public TaxClawAgent(IChatClient baseClient, string systemPrompt, IList<AITool> tools)
    {
        _client = baseClient.AsBuilder().UseFunctionInvocation().Build();
        _history = [new ChatMessage(ChatRole.System, systemPrompt)];
        _options = new ChatOptions { Tools = tools };
    }

    public async Task<string> SendAsync(string userMessage, CancellationToken ct = default)
    {
        _history.Add(new ChatMessage(ChatRole.User, userMessage));
        ChatResponse response = await _client.GetResponseAsync(_history, _options, ct);
        _history.AddRange(response.Messages);
        return response.Text;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: PASS — agent returns the canned text and the `add` tool is visible in forwarded options.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Agent/Prompts.cs src/TaxClaw.Agent/TaxClawAgent.cs tests/TaxClaw.Agent.Tests/TaxClawAgentTests.cs
git commit -m "feat(agent): add chat loop with function invocation and no-float guardrail"
```

---

### Task 9: Command router for the TUI

**Files:**
- Create: `src/TaxClaw.Agent/Commands/Commands.cs`
- Create: `src/TaxClaw.Agent/Commands/CommandRouter.cs`
- Test: `tests/TaxClaw.Agent.Tests/CommandRouterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Agent.Tests/CommandRouterTests.cs`:

```csharp
using TaxClaw.Agent.Commands;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class CommandRouterTests
{
    [Fact]
    public void Parses_new_project_command()
    {
        var command = CommandRouter.Parse("/new 2027");
        var newProject = Assert.IsType<NewProjectCommand>(command);
        Assert.Equal(TaxYear.Of(2027), newProject.Year);
    }

    [Fact]
    public void Parses_quit_command()
    {
        Assert.IsType<QuitCommand>(CommandRouter.Parse("/quit"));
    }

    [Fact]
    public void Plain_text_becomes_a_chat_command()
    {
        var command = CommandRouter.Parse("how are RSUs taxed?");
        var chat = Assert.IsType<ChatCommand>(command);
        Assert.Equal("how are RSUs taxed?", chat.Message);
    }

    [Fact]
    public void New_without_a_valid_year_becomes_an_error()
    {
        var command = CommandRouter.Parse("/new notayear");
        Assert.IsType<UnknownCommand>(command);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: FAIL — command types / `CommandRouter` do not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Agent/Commands/Commands.cs`:

```csharp
using TaxClaw.Core.Model;

namespace TaxClaw.Agent.Commands;

/// <summary>A parsed TUI input line.</summary>
public abstract record TuiCommand;

public sealed record NewProjectCommand(TaxYear Year) : TuiCommand;

public sealed record ChatCommand(string Message) : TuiCommand;

public sealed record QuitCommand : TuiCommand;

public sealed record UnknownCommand(string Reason) : TuiCommand;
```

Create `src/TaxClaw.Agent/Commands/CommandRouter.cs`:

```csharp
using TaxClaw.Core.Model;

namespace TaxClaw.Agent.Commands;

/// <summary>Pure mapping from a raw input line to a <see cref="TuiCommand"/> (no I/O).</summary>
public static class CommandRouter
{
    public static TuiCommand Parse(string line)
    {
        string trimmed = line.Trim();

        if (!trimmed.StartsWith('/'))
        {
            return new ChatCommand(trimmed);
        }

        string[] parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string verb = parts[0].ToLowerInvariant();

        return verb switch
        {
            "/quit" or "/exit" => new QuitCommand(),
            "/new" => ParseNew(parts),
            _ => new UnknownCommand($"Unknown command '{verb}'.")
        };
    }

    private static TuiCommand ParseNew(string[] parts)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out int year))
        {
            return new UnknownCommand("Usage: /new <year>, e.g. /new 2027");
        }

        try
        {
            return new NewProjectCommand(TaxYear.Of(year));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new UnknownCommand(ex.Message);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Agent.Tests`
Expected: PASS — all `CommandRouterTests` green.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Agent/Commands tests/TaxClaw.Agent.Tests/CommandRouterTests.cs
git commit -m "feat(agent): add TUI command router"
```

---

### Task 10: TUI host, configuration, and smoke test

**Files:**
- Add packages to `src/TaxClaw.Tui`
- Create: `src/TaxClaw.Tui/appsettings.json`
- Create: `src/TaxClaw.Tui/AppHost.cs`
- Modify: `src/TaxClaw.Tui/Program.cs`
- Create: `README.md`

- [ ] **Step 1: Add the TUI packages**

```bash
dotnet add src/TaxClaw.Tui package Spectre.Console
dotnet add src/TaxClaw.Tui package Microsoft.Extensions.Configuration
dotnet add src/TaxClaw.Tui package Microsoft.Extensions.Configuration.Json
dotnet add src/TaxClaw.Tui package Microsoft.Extensions.Configuration.EnvironmentVariables
```

- [ ] **Step 2: Create the configuration file**

Create `src/TaxClaw.Tui/appsettings.json`:

```json
{
  "Llm": {
    "Provider": "ollama",
    "Model": "llama3.1",
    "Endpoint": null,
    "ApiKey": null
  }
}
```

Make it copy to output — append this to `src/TaxClaw.Tui/TaxClaw.Tui.csproj` inside the existing `<Project>` element:

```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 3: Write the application host**

Create `src/TaxClaw.Tui/AppHost.cs`:

```csharp
using Spectre.Console;
using TaxClaw.Agent;
using TaxClaw.Agent.Commands;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;

namespace TaxClaw.Tui;

/// <summary>Drives the interactive loop: read a line, route it, act, print.</summary>
public sealed class AppHost(TaxClawAgent agent, IProfileStore profiles, IProjectStore projects)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        AnsiConsole.Write(new FigletText("tax-claw").Color(Color.Teal));
        AnsiConsole.MarkupLine("[grey]Type [/][teal]/new 2027[/][grey] to start a project, or just chat. [/][teal]/quit[/][grey] to exit.[/]");

        while (!ct.IsCancellationRequested)
        {
            string line = AnsiConsole.Prompt(new TextPrompt<string>("[teal]›[/]").AllowEmpty());
            TuiCommand command = CommandRouter.Parse(line);

            switch (command)
            {
                case QuitCommand:
                    return;

                case NewProjectCommand np:
                    await CreateProjectAsync(np.Year, ct);
                    break;

                case ChatCommand chat when chat.Message.Length > 0:
                    await AnsiConsole.Status().StartAsync("thinking…", async _ =>
                    {
                        string reply = await agent.SendAsync(chat.Message, ct);
                        AnsiConsole.MarkupLine($"[white]{Markup.Escape(reply)}[/]");
                    });
                    break;

                case UnknownCommand unknown:
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(unknown.Reason)}[/]");
                    break;
            }
        }
    }

    private async Task CreateProjectAsync(TaxYear year, CancellationToken ct)
    {
        Profile profile = await profiles.LoadAsync(ct) ?? PromptForProfile();

        // Re-confirm the profile for every new project, then snapshot it.
        if (!AnsiConsole.Confirm($"Use profile for [teal]{Markup.Escape(profile.FullName)}[/]?"))
        {
            profile = PromptForProfile();
        }

        await profiles.SaveAsync(profile, ct);

        try
        {
            var project = await projects.CreateAsync(year, profile, ct);
            AnsiConsole.MarkupLine($"[green]Created project[/] [teal]{project.Id}[/] [grey](status: {project.Status})[/].");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(ex.Message)}[/]");
        }
    }

    private static Profile PromptForProfile() => new()
    {
        FullName = AnsiConsole.Prompt(new TextPrompt<string>("Full name:")),
        RodneCislo = NullIfBlank(AnsiConsole.Prompt(new TextPrompt<string>("Rodné číslo (optional):").AllowEmpty())),
        Employer = NullIfBlank(AnsiConsole.Prompt(new TextPrompt<string>("Employer (optional):").AllowEmpty()))
    };

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
```

- [ ] **Step 4: Wire everything in `Program.cs`**

Replace the contents of `src/TaxClaw.Tui/Program.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using TaxClaw.Agent;
using TaxClaw.Llm;
using TaxClaw.Storage;
using TaxClaw.Tui;

IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables(prefix: "TAXCLAW_")
    .Build();

var llmOptions = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();

var chatClient = new ChatClientFactory(llmOptions).Create();
var agent = new TaxClawAgent(chatClient, Prompts.System, MathTools.CreateTools());

var root = new StorageRoot();
var profiles = new JsonProfileStore(root);
var projects = new JsonProjectStore(root);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await new AppHost(agent, profiles, projects).RunAsync(cts.Token);
```

- [ ] **Step 5: Verify the whole solution builds and all tests pass**

Run: `dotnet build`
Expected: `Build succeeded.`

Run: `dotnet test`
Expected: PASS — every test project green (Core, Storage, Llm, Agent).

- [ ] **Step 6: Manual smoke test**

Pre-req for live chat: a local Ollama with a model pulled (`ollama pull llama3.1`) and running. The project-creation flow needs no model.

Run: `dotnet run --project src/TaxClaw.Tui`

Verify:
1. The `tax-claw` banner renders.
2. Type `/new 2027`, answer the profile prompts, confirm — see "Created project 2027".
3. Confirm on disk: `ls ~/.tax-claw/projects/2027/project.json` exists.
4. Type `/new 2027` again — see the "already exists" warning.
5. (If Ollama is running) type `add 0.1 and 0.2 for me` — the reply uses the `add` tool and reports `0.3`.
6. Type `/quit` — the app exits.

- [ ] **Step 7: Write the README**

Create `README.md`:

````markdown
# tax-claw

An LLM-agent TUI that helps prepare a Czech personal income tax declaration
(form 25 5405). The agent orchestrates; deterministic code computes — the model
never does floating-point arithmetic itself.

See the design spec in [`docs/superpowers/specs/2026-06-15-tax-claw-design.md`](docs/superpowers/specs/2026-06-15-tax-claw-design.md).

## Requirements
- .NET 10 SDK
- (Optional, for chat) a running [Ollama](https://ollama.com) with a model pulled, e.g. `ollama pull llama3.1`

## Run

```bash
dotnet run --project src/TaxClaw.Tui
```

## Configure the LLM provider

Edit `src/TaxClaw.Tui/appsettings.json` (or set `TAXCLAW_Llm__*` env vars):

```json
{ "Llm": { "Provider": "ollama", "Model": "llama3.1" } }
```

Supported providers: `ollama`, `openai` (needs `ApiKey`), `azure` (needs `Endpoint` + `ApiKey`).

## Test

```bash
dotnet test
```

## Data location

Projects and profile are stored under `~/.tax-claw/`.
````

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(tui): wire Spectre.Console host, config, and project flow"
```

---

## Self-Review

**1. Spec coverage (Plan 1 portion):**
- Provider-agnostic LLM (`IChatClient`, switchable provider) → Task 6. ✓
- Local storage of profile + per-year projects; profile re-confirmed per project → Tasks 4, 5, 10. ✓
- Decimal-only math, agent never does float; primitives `add/subtract/multiply/divide/round` → Tasks 2, 3, 7, 8. ✓
- CZ rounding (`zaokrouhlení`) → Task 3. ✓
- TUI chat "as Claude Code" with tool-calling → Tasks 8, 10. ✓
- Project lifecycle status (`Draft…Filed`) seed → Task 4. ✓
- Stable substrate now, MAF later via same seam → Architecture note + Task 8 doc. ✓
- *Deferred to later plans (correctly out of scope here):* document pipeline, law RAG, generated calc functions, memory, skills/MCP/sharing, PII middleware, exporters. These are the remaining roadmap plans.

**2. Placeholder scan:** No "TBD/TODO/handle edge cases" — every code step contains complete, compilable code and every test contains real assertions. The one conditional note (`AsIChatClient` vs `AsChatClient`) is concrete recovery guidance, not a placeholder. ✓

**3. Type consistency:** Names verified across tasks — `DecimalMath.RoundToUnit(decimal, decimal, RoundingDirection)` (Tasks 2, 3, 7); `MathTools` tool names `add/subtract/multiply/divide/round_to_unit` (Task 7) match the agent-forwarding assertion (Task 8); `StorageRoot`, `JsonProfileStore`, `JsonProjectStore` consistent (Tasks 5, 10); `IProfileStore`/`IProjectStore` signatures match host usage (Tasks 5, 10); `TuiCommand` subtypes `NewProjectCommand/ChatCommand/QuitCommand/UnknownCommand` consistent (Tasks 9, 10); `LlmOptions`/`ChatClientFactory` consistent (Tasks 6, 10). ✓

---

## Notes carried to later plans
- Open questions from spec §14 (default dev provider, EPO XSD source, legislation source/update policy, macOS sandbox tech, skill manifest schema) are resolved in their owning plans (3, 4, 6, 7).
- The `IChatClient` seam in `TaxClawAgent` is the intended swap point for the Microsoft Agent Framework agent (Plan 2) and the PII-redaction middleware (Plan 7).
