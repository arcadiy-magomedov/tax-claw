# tax-claw Canonical Model & Calc Runtime — Implementation Plan (Plan 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the deterministic calculation core — a canonical `TaxReturn`, a form modeled as a dependency graph of lines, and an engine that computes each line by executing **approved, version-pinned functions** (never the agent doing math), emitting a full `CalculationTrace` for every figure.

**Architecture:** Pure value types (`Money`, `Provenance`, `Derivation`, `CalculationTrace`) live in `TaxClaw.Core`. A new `TaxClaw.Calc` library holds the form DAG, the calc-function registry (keyed by line + law/form version + source hash), and the `CalcEngine` that topologically evaluates lines and records traces. A `TaxClaw.Calc.Scripting` library compiles agent-generated function source with Roslyn under a timeout, behind an `ApprovalGate` that hashes and pins sources before they can run. **Boundary:** the engine only ever runs *registered, approved* functions; generation/approval is a separate, explicit step.

**Tech Stack:** .NET 10, `Microsoft.CodeAnalysis.CSharp.Scripting` (Roslyn), xUnit. Builds on Plan 1 (`TaxClaw.Core`, `DecimalMath`).

---

## File Structure

- `src/TaxClaw.Core/Model/Money.cs` — currency-tagged decimal value object.
- `src/TaxClaw.Core/Calc/Provenance.cs` — source citation (§ / form line / doc id + hash + version).
- `src/TaxClaw.Core/Calc/CalculationTrace.cs` — `CalculationStep`, `Derivation`, `CalculationTrace`.
- `src/TaxClaw.Core/Model/TaxReturn.cs` — canonical model: income items + form line values.
- `src/TaxClaw.Calc/Form/FormLineId.cs`, `Form/FormLineDefinition.cs`, `Form/FormDefinition.cs` — the DAG.
- `src/TaxClaw.Calc/Form/TopologicalSort.cs` — dependency ordering.
- `src/TaxClaw.Calc/Functions/CalcContext.cs` — read inputs / call math during a line's calc.
- `src/TaxClaw.Calc/Functions/ICalcFunction.cs`, `Functions/CalcFunctionKey.cs`, `Functions/CalcFunctionRegistry.cs`.
- `src/TaxClaw.Calc/CalcEngine.cs` — evaluate the DAG, produce traces.
- `src/TaxClaw.Calc.Scripting/FunctionSource.cs` — source + provenance + hash.
- `src/TaxClaw.Calc.Scripting/ApprovalGate.cs` — approve/verify by hash.
- `src/TaxClaw.Calc.Scripting/ScriptCompiler.cs` — Roslyn compile to an `ICalcFunction` with timeout.
- Tests mirror each under `tests/TaxClaw.Calc.Tests/` and `tests/TaxClaw.Core.Tests/`.

---

### Task 1: Project scaffold for the calc libraries

**Files:**
- Create: `src/TaxClaw.Calc`, `src/TaxClaw.Calc.Scripting`, `tests/TaxClaw.Calc.Tests`

- [ ] **Step 1: Create and reference the projects**

```bash
dotnet new classlib -o src/TaxClaw.Calc
dotnet new classlib -o src/TaxClaw.Calc.Scripting
dotnet new xunit    -o tests/TaxClaw.Calc.Tests
rm src/TaxClaw.Calc/Class1.cs src/TaxClaw.Calc.Scripting/Class1.cs tests/TaxClaw.Calc.Tests/UnitTest1.cs

dotnet sln add src/TaxClaw.Calc src/TaxClaw.Calc.Scripting tests/TaxClaw.Calc.Tests

dotnet add src/TaxClaw.Calc reference src/TaxClaw.Core
dotnet add src/TaxClaw.Calc.Scripting reference src/TaxClaw.Core src/TaxClaw.Calc
dotnet add src/TaxClaw.Calc.Scripting package Microsoft.CodeAnalysis.CSharp.Scripting
dotnet add tests/TaxClaw.Calc.Tests reference src/TaxClaw.Core src/TaxClaw.Calc src/TaxClaw.Calc.Scripting
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore(calc): scaffold calc and scripting libraries"
```

---

### Task 2: Money value object

**Files:**
- Create: `src/TaxClaw.Core/Model/Money.cs`
- Test: `tests/TaxClaw.Core.Tests/MoneyTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Core.Tests/MoneyTests.cs`:

```csharp
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Core.Tests;

public class MoneyTests
{
    [Fact]
    public void Adding_same_currency_sums_amounts()
    {
        var sum = Money.Czk(100m).Add(Money.Czk(50m));
        Assert.Equal(Money.Czk(150m), sum);
    }

    [Fact]
    public void Adding_different_currencies_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Money.Czk(100m).Add(new Money(10m, "USD")));
    }

    [Fact]
    public void Currency_is_normalized_to_upper_case()
    {
        Assert.Equal("USD", new Money(1m, "usd").Currency);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: FAIL — `Money` does not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Core/Model/Money.cs`:

```csharp
using TaxClaw.Core.Math;

namespace TaxClaw.Core.Model;

/// <summary>A currency-tagged exact decimal amount. All arithmetic stays in decimal.</summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public string Currency { get; } = Currency.ToUpperInvariant();

    public static Money Czk(decimal amount) => new(amount, "CZK");

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = DecimalMath.Add(Amount, other.Amount) };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = DecimalMath.Subtract(Amount, other.Amount) };
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine {Currency} with {other.Currency}; convert first.");
        }
    }

    public override string ToString() => $"{Amount} {Currency}";
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Core/Model/Money.cs tests/TaxClaw.Core.Tests/MoneyTests.cs
git commit -m "feat(core): add Money value object"
```

---

### Task 3: Provenance and calculation trace types

**Files:**
- Create: `src/TaxClaw.Core/Calc/Provenance.cs`
- Create: `src/TaxClaw.Core/Calc/CalculationTrace.cs`
- Test: `tests/TaxClaw.Core.Tests/CalculationTraceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Core.Tests/CalculationTraceTests.cs`:

```csharp
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Core.Tests;

public class CalculationTraceTests
{
    [Fact]
    public void Trace_renders_steps_in_order_for_explanation()
    {
        var trace = new CalculationTrace("r38", new[]
        {
            new CalculationStep("read r36", "120000"),
            new CalculationStep("subtract r37", "120000 - 20000 = 100000")
        }, "100000", new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1", Hash: "abc"));

        Assert.Equal("r38", trace.LineId);
        Assert.Equal("100000", trace.Result);
        Assert.Equal(2, trace.Steps.Count);
        Assert.Contains("§ 16", trace.Provenance.ToString());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: FAIL — `Provenance` / `CalculationStep` / `CalculationTrace` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Core/Calc/Provenance.cs`:

```csharp
namespace TaxClaw.Core.Calc;

/// <summary>
/// Where a figure or rule comes from. Every computed number must carry one of these so any
/// result can be traced back to legislation, a form line, and the exact pinned version.
/// </summary>
public sealed record Provenance(
    string? LawRef = null,
    string? FormLine = null,
    string? DocumentId = null,
    string? Version = null,
    string? Hash = null)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (LawRef is not null) parts.Add(LawRef);
        if (FormLine is not null) parts.Add($"line {FormLine}");
        if (DocumentId is not null) parts.Add($"doc {DocumentId}");
        if (Version is not null) parts.Add($"v{Version}");
        return parts.Count == 0 ? "(no source)" : string.Join(", ", parts);
    }
}
```

Create `src/TaxClaw.Core/Calc/CalculationTrace.cs`:

```csharp
namespace TaxClaw.Core.Calc;

/// <summary>One human-readable step in deriving a figure.</summary>
public sealed record CalculationStep(string Description, string Detail);

/// <summary>
/// The full derivation of a single form line: ordered steps, the final result, and the
/// provenance. This is what powers "explain how line N was computed".
/// </summary>
public sealed record CalculationTrace(
    string LineId,
    IReadOnlyList<CalculationStep> Steps,
    string Result,
    Provenance Provenance);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Core/Calc tests/TaxClaw.Core.Tests/CalculationTraceTests.cs
git commit -m "feat(core): add provenance and calculation trace types"
```

---

### Task 4: TaxReturn canonical model

**Files:**
- Create: `src/TaxClaw.Core/Model/TaxReturn.cs`
- Test: `tests/TaxClaw.Core.Tests/TaxReturnTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Core.Tests/TaxReturnTests.cs`:

```csharp
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Core.Tests;

public class TaxReturnTests
{
    [Fact]
    public void Setting_a_line_stores_value_and_trace()
    {
        var ret = new TaxReturn(TaxYear.Of(2027));
        var trace = new CalculationTrace("r38",
            new[] { new CalculationStep("x", "y") }, "100000",
            new Provenance(FormLine: "r38"));

        ret = ret.WithLine("r38", 100000m, trace);

        Assert.Equal(100000m, ret.GetLine("r38"));
        Assert.Equal(trace, ret.GetTrace("r38"));
    }

    [Fact]
    public void Getting_an_unset_line_returns_null()
    {
        var ret = new TaxReturn(TaxYear.Of(2027));
        Assert.Null(ret.GetLine("r99"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: FAIL — `TaxReturn` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Core/Model/TaxReturn.cs`:

```csharp
using System.Collections.Immutable;
using TaxClaw.Core.Calc;

namespace TaxClaw.Core.Model;

/// <summary>An income item feeding the declaration (employment, RSU vest, dividend, sale).</summary>
public sealed record IncomeItem(
    string Kind,
    Money Amount,
    DateOnly Date,
    string? DocumentId = null);

/// <summary>
/// The format-independent single source of truth for a declaration. Form-specific exports
/// (summary, PDF, XML) are projections of this model. Immutable — each update returns a copy.
/// </summary>
public sealed record TaxReturn
{
    public TaxReturn(TaxYear year)
    {
        Year = year;
        Incomes = [];
        Lines = ImmutableDictionary<string, decimal>.Empty;
        Traces = ImmutableDictionary<string, CalculationTrace>.Empty;
    }

    public TaxYear Year { get; init; }
    public ImmutableList<IncomeItem> Incomes { get; init; }
    public ImmutableDictionary<string, decimal> Lines { get; init; }
    public ImmutableDictionary<string, CalculationTrace> Traces { get; init; }

    public TaxReturn WithIncome(IncomeItem item) =>
        this with { Incomes = Incomes.Add(item) };

    public TaxReturn WithLine(string lineId, decimal value, CalculationTrace trace) =>
        this with
        {
            Lines = Lines.SetItem(lineId, value),
            Traces = Traces.SetItem(lineId, trace)
        };

    public decimal? GetLine(string lineId) =>
        Lines.TryGetValue(lineId, out var v) ? v : null;

    public CalculationTrace? GetTrace(string lineId) =>
        Traces.TryGetValue(lineId, out var t) ? t : null;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Core/Model/TaxReturn.cs tests/TaxClaw.Core.Tests/TaxReturnTests.cs
git commit -m "feat(core): add TaxReturn canonical model"
```

---

### Task 5: Form definition and topological ordering

**Files:**
- Create: `src/TaxClaw.Calc/Form/FormLineId.cs`
- Create: `src/TaxClaw.Calc/Form/FormLineDefinition.cs`
- Create: `src/TaxClaw.Calc/Form/FormDefinition.cs`
- Create: `src/TaxClaw.Calc/Form/TopologicalSort.cs`
- Test: `tests/TaxClaw.Calc.Tests/FormDefinitionTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Calc.Tests/FormDefinitionTests.cs`:

```csharp
using TaxClaw.Calc.Form;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class FormDefinitionTests
{
    [Fact]
    public void Evaluation_order_respects_dependencies()
    {
        var form = new FormDefinition("25 5405", "2027.1", new[]
        {
            new FormLineDefinition("r38", new[] { "r36", "r37" }),
            new FormLineDefinition("r36", Array.Empty<string>()),
            new FormLineDefinition("r37", Array.Empty<string>())
        });

        var order = form.EvaluationOrder().Select(l => l.Id).ToList();

        Assert.True(order.IndexOf("r36") < order.IndexOf("r38"));
        Assert.True(order.IndexOf("r37") < order.IndexOf("r38"));
    }

    [Fact]
    public void A_dependency_cycle_is_rejected()
    {
        var form = new FormDefinition("x", "1", new[]
        {
            new FormLineDefinition("a", new[] { "b" }),
            new FormLineDefinition("b", new[] { "a" })
        });

        Assert.Throws<InvalidOperationException>(() => form.EvaluationOrder());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: FAIL — form types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Calc/Form/FormLineId.cs`:

```csharp
namespace TaxClaw.Calc.Form;

/// <summary>The identifier of a form line (řádek), e.g. "r38".</summary>
public readonly record struct FormLineId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator string(FormLineId id) => id.Value;
}
```

Create `src/TaxClaw.Calc/Form/FormLineDefinition.cs`:

```csharp
namespace TaxClaw.Calc.Form;

/// <summary>A single line and the line ids it depends on (its inputs in the DAG).</summary>
public sealed record FormLineDefinition(string Id, IReadOnlyList<string> DependsOn);
```

Create `src/TaxClaw.Calc/Form/FormDefinition.cs`:

```csharp
namespace TaxClaw.Calc.Form;

/// <summary>
/// An official form modeled as a dependency graph of lines. Parsed from the form's filling
/// instructions (pokyny) in the document-pipeline plan; here it is a plain in-memory DAG.
/// </summary>
public sealed class FormDefinition(string formCode, string version, IReadOnlyList<FormLineDefinition> lines)
{
    public string FormCode { get; } = formCode;
    public string Version { get; } = version;
    public IReadOnlyList<FormLineDefinition> Lines { get; } = lines;

    /// <summary>Lines in an order where every dependency precedes its dependents.</summary>
    public IReadOnlyList<FormLineDefinition> EvaluationOrder() => TopologicalSort.Order(Lines);
}
```

Create `src/TaxClaw.Calc/Form/TopologicalSort.cs`:

```csharp
namespace TaxClaw.Calc.Form;

internal static class TopologicalSort
{
    public static IReadOnlyList<FormLineDefinition> Order(IReadOnlyList<FormLineDefinition> lines)
    {
        var byId = lines.ToDictionary(l => l.Id);
        var visited = new Dictionary<string, bool>(); // false = in progress, true = done
        var result = new List<FormLineDefinition>();

        void Visit(FormLineDefinition line)
        {
            if (visited.TryGetValue(line.Id, out bool done))
            {
                if (!done)
                {
                    throw new InvalidOperationException($"Dependency cycle detected at line '{line.Id}'.");
                }
                return;
            }

            visited[line.Id] = false;
            foreach (string dep in line.DependsOn)
            {
                if (byId.TryGetValue(dep, out var depLine))
                {
                    Visit(depLine);
                }
            }
            visited[line.Id] = true;
            result.Add(line);
        }

        foreach (var line in lines)
        {
            Visit(line);
        }

        return result;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Calc/Form tests/TaxClaw.Calc.Tests/FormDefinitionTests.cs
git commit -m "feat(calc): add form DAG and topological ordering"
```

---

### Task 6: Calc function abstraction, context, and registry

**Files:**
- Create: `src/TaxClaw.Calc/Functions/CalcContext.cs`
- Create: `src/TaxClaw.Calc/Functions/ICalcFunction.cs`
- Create: `src/TaxClaw.Calc/Functions/CalcFunctionKey.cs`
- Create: `src/TaxClaw.Calc/Functions/CalcFunctionRegistry.cs`
- Test: `tests/TaxClaw.Calc.Tests/CalcFunctionRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Calc.Tests/CalcFunctionRegistryTests.cs`:

```csharp
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class CalcFunctionRegistryTests
{
    private sealed class ConstFunction(decimal value) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            steps = new[] { new CalculationStep("const", value.ToString()) };
            return value;
        }
    }

    [Fact]
    public void Resolves_a_registered_function_by_line_and_version()
    {
        var registry = new CalcFunctionRegistry();
        var key = new CalcFunctionKey("r36", "2027.1");
        registry.Register(key, new ConstFunction(120000m));

        Assert.True(registry.TryResolve(key, out var fn));
        Assert.NotNull(fn);
    }

    [Fact]
    public void A_different_version_does_not_resolve()
    {
        var registry = new CalcFunctionRegistry();
        registry.Register(new CalcFunctionKey("r36", "2027.1"), new ConstFunction(1m));

        Assert.False(registry.TryResolve(new CalcFunctionKey("r36", "2026.1"), out _));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: FAIL — calc-function types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Calc/Functions/CalcContext.cs`:

```csharp
using TaxClaw.Core.Math;

namespace TaxClaw.Calc.Functions;

/// <summary>
/// What a calc function may touch while computing a line: previously computed line values and
/// the sanctioned decimal math. Functions never reach outside this surface.
/// </summary>
public sealed class CalcContext(IReadOnlyDictionary<string, decimal> lines)
{
    public decimal Line(string id) =>
        lines.TryGetValue(id, out var v)
            ? v
            : throw new KeyNotFoundException($"Line '{id}' has not been computed yet.");

    public decimal Add(decimal a, decimal b) => DecimalMath.Add(a, b);
    public decimal Subtract(decimal a, decimal b) => DecimalMath.Subtract(a, b);
    public decimal Multiply(decimal a, decimal b) => DecimalMath.Multiply(a, b);
    public decimal Divide(decimal a, decimal b) => DecimalMath.Divide(a, b);
    public decimal RoundToUnit(decimal value, decimal unit, RoundingDirection direction) =>
        DecimalMath.RoundToUnit(value, unit, direction);
}
```

Create `src/TaxClaw.Calc/Functions/ICalcFunction.cs`:

```csharp
using TaxClaw.Core.Calc;

namespace TaxClaw.Calc.Functions;

/// <summary>
/// Computes one form line deterministically. Implementations are either hand-written (for tests)
/// or compiled from approved, agent-generated source. They return the value and the human-readable
/// steps that become part of the <see cref="CalculationTrace"/>.
/// </summary>
public interface ICalcFunction
{
    decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps);
}
```

Create `src/TaxClaw.Calc/Functions/CalcFunctionKey.cs`:

```csharp
namespace TaxClaw.Calc.Functions;

/// <summary>Identifies a calc function by the form line it computes and the pinned version.</summary>
public readonly record struct CalcFunctionKey(string LineId, string Version);
```

Create `src/TaxClaw.Calc/Functions/CalcFunctionRegistry.cs`:

```csharp
namespace TaxClaw.Calc.Functions;

/// <summary>
/// Holds approved calc functions keyed by line + version. The engine only runs functions that
/// were registered here, so version pinning is enforced at lookup time.
/// </summary>
public sealed class CalcFunctionRegistry
{
    private readonly Dictionary<CalcFunctionKey, ICalcFunction> _functions = new();

    public void Register(CalcFunctionKey key, ICalcFunction function) =>
        _functions[key] = function;

    public bool TryResolve(CalcFunctionKey key, out ICalcFunction? function) =>
        _functions.TryGetValue(key, out function);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Calc/Functions tests/TaxClaw.Calc.Tests/CalcFunctionRegistryTests.cs
git commit -m "feat(calc): add calc function abstraction and registry"
```

---

### Task 7: CalcEngine — evaluate the DAG and produce traces

**Files:**
- Create: `src/TaxClaw.Calc/CalcEngine.cs`
- Test: `tests/TaxClaw.Calc.Tests/CalcEngineTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Calc.Tests/CalcEngineTests.cs`:

```csharp
using TaxClaw.Calc;
using TaxClaw.Calc.Form;
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class CalcEngineTests
{
    private sealed class ConstFunction(decimal value) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            steps = new[] { new CalculationStep("const", value.ToString()) };
            return value;
        }
    }

    private sealed class SubtractFunction(string a, string b) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            decimal result = context.Subtract(context.Line(a), context.Line(b));
            steps = new[]
            {
                new CalculationStep($"{a} - {b}", $"{context.Line(a)} - {context.Line(b)} = {result}")
            };
            return result;
        }
    }

    [Fact]
    public void Computes_dependent_line_and_records_traces()
    {
        var form = new FormDefinition("25 5405", "2027.1", new[]
        {
            new FormLineDefinition("r36", Array.Empty<string>()),
            new FormLineDefinition("r37", Array.Empty<string>()),
            new FormLineDefinition("r38", new[] { "r36", "r37" })
        });

        var registry = new CalcFunctionRegistry();
        registry.Register(new CalcFunctionKey("r36", "2027.1"), new ConstFunction(120000m));
        registry.Register(new CalcFunctionKey("r37", "2027.1"), new ConstFunction(20000m));
        registry.Register(new CalcFunctionKey("r38", "2027.1"), new SubtractFunction("r36", "r37"));

        var engine = new CalcEngine(registry);
        TaxReturn result = engine.Evaluate(form, new TaxReturn(TaxYear.Of(2027)));

        Assert.Equal(100000m, result.GetLine("r38"));
        Assert.Equal("r38", result.GetTrace("r38")!.LineId);
        Assert.Equal("2027.1", result.GetTrace("r38")!.Provenance.Version);
    }

    [Fact]
    public void Missing_function_for_a_line_throws_with_the_line_id()
    {
        var form = new FormDefinition("x", "2027.1", new[]
        {
            new FormLineDefinition("r1", Array.Empty<string>())
        });

        var engine = new CalcEngine(new CalcFunctionRegistry());

        var ex = Assert.Throws<InvalidOperationException>(
            () => engine.Evaluate(form, new TaxReturn(TaxYear.Of(2027))));
        Assert.Contains("r1", ex.Message);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: FAIL — `CalcEngine` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Calc/CalcEngine.cs`:

```csharp
using TaxClaw.Calc.Form;
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;

namespace TaxClaw.Calc;

/// <summary>
/// Evaluates a form by running the approved function for each line in dependency order, threading
/// computed values forward and recording a <see cref="CalculationTrace"/> for every line.
/// The engine performs no arithmetic of its own — it only orchestrates function execution.
/// </summary>
public sealed class CalcEngine(CalcFunctionRegistry registry)
{
    public TaxReturn Evaluate(FormDefinition form, TaxReturn seed)
    {
        TaxReturn current = seed;
        var computed = new Dictionary<string, decimal>();

        foreach (FormLineDefinition line in form.EvaluationOrder())
        {
            var key = new CalcFunctionKey(line.Id, form.Version);
            if (!registry.TryResolve(key, out var function) || function is null)
            {
                throw new InvalidOperationException(
                    $"No approved calc function registered for line '{line.Id}' (version {form.Version}).");
            }

            var context = new CalcContext(computed);
            decimal value = function.Evaluate(context, out var steps);
            computed[line.Id] = value;

            var trace = new CalculationTrace(
                line.Id,
                steps,
                value.ToString(),
                new Provenance(FormLine: line.Id, Version: form.Version));

            current = current.WithLine(line.Id, value, trace);
        }

        return current;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: PASS — `r38` computes to `100000`, traces recorded, missing-function path throws.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Calc/CalcEngine.cs tests/TaxClaw.Calc.Tests/CalcEngineTests.cs
git commit -m "feat(calc): add CalcEngine that evaluates form DAG with traces"
```

---

### Task 8: Function source, hashing, and approval gate

**Files:**
- Create: `src/TaxClaw.Calc.Scripting/FunctionSource.cs`
- Create: `src/TaxClaw.Calc.Scripting/ApprovalGate.cs`
- Test: `tests/TaxClaw.Calc.Tests/ApprovalGateTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Calc.Tests/ApprovalGateTests.cs`:

```csharp
using TaxClaw.Calc.Scripting;
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class ApprovalGateTests
{
    private static FunctionSource Source(string body) =>
        new("r38", "2027.1", body, new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));

    [Fact]
    public void Hash_is_stable_for_identical_source()
    {
        Assert.Equal(Source("return 1m;").Hash, Source("return 1m;").Hash);
    }

    [Fact]
    public void Hash_differs_when_body_changes()
    {
        Assert.NotEqual(Source("return 1m;").Hash, Source("return 2m;").Hash);
    }

    [Fact]
    public void Unapproved_source_is_not_approved()
    {
        var gate = new ApprovalGate();
        Assert.False(gate.IsApproved(Source("return 1m;")));
    }

    [Fact]
    public void Approving_marks_exactly_that_source_hash()
    {
        var gate = new ApprovalGate();
        var src = Source("return 1m;");

        gate.Approve(src);

        Assert.True(gate.IsApproved(src));
        Assert.False(gate.IsApproved(Source("return 999m;")));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: FAIL — `FunctionSource` / `ApprovalGate` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Calc.Scripting/FunctionSource.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using TaxClaw.Core.Calc;

namespace TaxClaw.Calc.Scripting;

/// <summary>
/// Agent-generated calc-function source plus its provenance. The <see cref="Hash"/> pins the exact
/// text + line + version so an approval can never silently apply to changed code.
/// </summary>
public sealed record FunctionSource(string LineId, string Version, string Body, Provenance Provenance)
{
    public string Hash { get; } = ComputeHash(LineId, Version, Body);

    private static string ComputeHash(string lineId, string version, string body)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{lineId}\n{version}\n{body}"));
        return Convert.ToHexStringLower(bytes);
    }
}
```

Create `src/TaxClaw.Calc.Scripting/ApprovalGate.cs`:

```csharp
namespace TaxClaw.Calc.Scripting;

/// <summary>
/// Records which generated sources a human has approved, by hash. Compilation/registration of a
/// function is gated on approval, so no unapproved code can be promoted into the engine.
/// </summary>
public sealed class ApprovalGate
{
    private readonly HashSet<string> _approvedHashes = new();

    public void Approve(FunctionSource source) => _approvedHashes.Add(source.Hash);

    public bool IsApproved(FunctionSource source) => _approvedHashes.Contains(source.Hash);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Calc.Scripting/FunctionSource.cs src/TaxClaw.Calc.Scripting/ApprovalGate.cs tests/TaxClaw.Calc.Tests/ApprovalGateTests.cs
git commit -m "feat(scripting): add function source hashing and approval gate"
```

---

### Task 9: Roslyn script compiler with timeout and approval enforcement

**Files:**
- Create: `src/TaxClaw.Calc.Scripting/ScriptCompiler.cs`
- Test: `tests/TaxClaw.Calc.Tests/ScriptCompilerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Calc.Tests/ScriptCompilerTests.cs`:

```csharp
using TaxClaw.Calc.Functions;
using TaxClaw.Calc.Scripting;
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class ScriptCompilerTests
{
    private static FunctionSource Source(string body) =>
        new("r38", "2027.1", body, new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));

    [Fact]
    public async Task Compiling_unapproved_source_throws()
    {
        var compiler = new ScriptCompiler(new ApprovalGate());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => compiler.CompileAsync(Source("return 1m;")));
    }

    [Fact]
    public async Task Compiled_function_reads_lines_and_uses_context_math()
    {
        var gate = new ApprovalGate();
        // Body has access to `ctx` (CalcContext); it must use ctx math, not raw operators on doubles.
        var src = Source("return ctx.Subtract(ctx.Line(\"r36\"), ctx.Line(\"r37\"));");
        gate.Approve(src);

        var compiler = new ScriptCompiler(gate);
        ICalcFunction fn = await compiler.CompileAsync(src);

        var ctx = new CalcContext(new Dictionary<string, decimal> { ["r36"] = 120000m, ["r37"] = 20000m });
        decimal value = fn.Evaluate(ctx, out var steps);

        Assert.Equal(100000m, value);
        Assert.NotEmpty(steps);
    }

    [Fact]
    public async Task Long_running_script_is_aborted_by_the_timeout()
    {
        var gate = new ApprovalGate();
        var src = Source("while(true) {} ");
        gate.Approve(src);

        var compiler = new ScriptCompiler(gate, TimeSpan.FromMilliseconds(200));
        ICalcFunction fn = await compiler.CompileAsync(src);

        var ctx = new CalcContext(new Dictionary<string, decimal>());
        Assert.Throws<TimeoutException>(() => fn.Evaluate(ctx, out _));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: FAIL — `ScriptCompiler` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Calc.Scripting/ScriptCompiler.cs`:

```csharp
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;

namespace TaxClaw.Calc.Scripting;

/// <summary>
/// Compiles an approved <see cref="FunctionSource"/> body into an <see cref="ICalcFunction"/>.
/// The body is a C# expression/statements with a single global, <c>ctx</c> (a
/// <see cref="CalcContext"/>), returning a <see cref="decimal"/>. Execution is bounded by a
/// timeout. This is an in-process guard; OS-level sandboxing is layered on in the document plan.
/// </summary>
public sealed class ScriptCompiler(ApprovalGate gate, TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(2);

    public sealed class Globals
    {
        public required CalcContext ctx { get; init; }
    }

    public async Task<ICalcFunction> CompileAsync(FunctionSource source)
    {
        if (!gate.IsApproved(source))
        {
            throw new InvalidOperationException(
                $"Refusing to compile unapproved function for line '{source.LineId}'.");
        }

        ScriptOptions options = ScriptOptions.Default
            .WithImports("System")
            .AddReferences(typeof(CalcContext).Assembly);

        // Compile once up front so syntax errors surface immediately.
        Script<decimal> script = CSharpScript.Create<decimal>(source.Body, options, typeof(Globals));
        script.Compile();

        return new CompiledCalcFunction(script, source, _timeout);
    }

    private sealed class CompiledCalcFunction(Script<decimal> script, FunctionSource source, TimeSpan timeout)
        : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            var globals = new Globals { ctx = context };

            // Offload to the thread pool so a CPU-bound body cannot block the caller, and bound it
            // with a wait. CSharpScript runs synchronously up to its first await, so a tight
            // `while(true)` would otherwise never yield; Task.Run + Wait(timeout) makes the timeout
            // reliable. A runaway body is abandoned (it cannot be force-killed in-process) — the
            // real, killable sandbox is the OS-level process introduced in the document plan.
            Task<ScriptState<decimal>> run = Task.Run(() => script.RunAsync(globals));

            if (!run.Wait(timeout))
            {
                throw new TimeoutException(
                    $"Calc function for line '{source.LineId}' exceeded {timeout.TotalMilliseconds} ms.");
            }

            decimal value = run.GetAwaiter().GetResult().ReturnValue;
            steps = new[]
            {
                new CalculationStep(
                    $"evaluate {source.LineId} ({source.Provenance})",
                    value.ToString())
            };
            return value;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: PASS — unapproved compile throws; approved function computes `100000`; the infinite-loop body is abandoned after the timeout and `Evaluate` throws `TimeoutException`.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Calc.Scripting/ScriptCompiler.cs tests/TaxClaw.Calc.Tests/ScriptCompilerTests.cs
git commit -m "feat(scripting): compile approved calc functions with a timeout"
```

---

### Task 10: End-to-end — generated function drives the engine

**Files:**
- Test: `tests/TaxClaw.Calc.Tests/EndToEndCalcTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Calc.Tests/EndToEndCalcTests.cs`:

```csharp
using TaxClaw.Calc;
using TaxClaw.Calc.Form;
using TaxClaw.Calc.Functions;
using TaxClaw.Calc.Scripting;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class EndToEndCalcTests
{
    private sealed class ConstFunction(decimal value) : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            steps = new[] { new CalculationStep("input", value.ToString()) };
            return value;
        }
    }

    [Fact]
    public async Task Approved_generated_subtraction_computes_a_line_with_trace()
    {
        // Approve + compile a generated function for r38.
        var gate = new ApprovalGate();
        var src = new FunctionSource(
            "r38", "2027.1",
            "return ctx.Subtract(ctx.Line(\"r36\"), ctx.Line(\"r37\"));",
            new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));
        gate.Approve(src);
        ICalcFunction r38 = await new ScriptCompiler(gate).CompileAsync(src);

        var registry = new CalcFunctionRegistry();
        registry.Register(new CalcFunctionKey("r36", "2027.1"), new ConstFunction(120000m));
        registry.Register(new CalcFunctionKey("r37", "2027.1"), new ConstFunction(20000m));
        registry.Register(new CalcFunctionKey("r38", "2027.1"), r38);

        var form = new FormDefinition("25 5405", "2027.1", new[]
        {
            new FormLineDefinition("r36", Array.Empty<string>()),
            new FormLineDefinition("r37", Array.Empty<string>()),
            new FormLineDefinition("r38", new[] { "r36", "r37" })
        });

        TaxReturn result = new CalcEngine(registry).Evaluate(form, new TaxReturn(TaxYear.Of(2027)));

        Assert.Equal(100000m, result.GetLine("r38"));
        Assert.Equal("r38", result.GetTrace("r38")!.LineId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails, then passes**

Run: `dotnet test tests/TaxClaw.Calc.Tests`
Expected: With all prior tasks complete, this compiles and PASSES. (If types are missing, complete earlier tasks first.)

- [ ] **Step 3: Run the full suite**

Run: `dotnet test`
Expected: PASS — every project green.

- [ ] **Step 4: Commit**

```bash
git add tests/TaxClaw.Calc.Tests/EndToEndCalcTests.cs
git commit -m "test(calc): end-to-end approved generated function through the engine"
```

---

## Self-Review

**1. Spec coverage:**
- Canonical `TaxReturn` as single source of truth → Task 4. ✓
- Form as DAG of lines (řádky), dependency ordering → Task 5. ✓
- Calc via approved functions, never agent float math → Tasks 6, 7, 9. ✓
- Decimal-only arithmetic inside calc (`CalcContext` exposes only `DecimalMath`) → Task 6. ✓
- Generation → tests/approval → pinning (version + hash) → execution → Tasks 8, 9. ✓
- `CalculationTrace` with provenance for "explain" → Tasks 3, 7. ✓
- Money value object for currency-tagged amounts → Task 2. ✓
- Sandbox (in-process timeout now; OS-level deferred to document plan, noted) → Task 9. ✓

**2. Placeholder scan:** No TBD/TODO. Every step has complete code and real assertions. The two conditional notes (timeout-thread fallback; `Convert.ToHexStringLower` availability) are concrete recovery guidance. ✓

**3. Type consistency:** `ICalcFunction.Evaluate(CalcContext, out IReadOnlyList<CalculationStep>)` identical across Tasks 6, 7, 9, 10. `CalcFunctionKey(LineId, Version)` consistent (6, 7, 10). `FunctionSource(LineId, Version, Body, Provenance)` + `.Hash` consistent (8, 9, 10). `FormDefinition(formCode, version, lines)` + `.Version`/`.EvaluationOrder()` consistent (5, 7, 10). `TaxReturn.WithLine/GetLine/GetTrace` consistent (4, 7, 10). `Provenance(LawRef, FormLine, DocumentId, Version, Hash)` consistent throughout. ✓

> If `Convert.ToHexStringLower` is unavailable on the SDK, use `Convert.ToHexString(bytes).ToLowerInvariant()`.
