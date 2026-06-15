# tax-claw Privacy & Exporters — Implementation Plan (Plan 7)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Protect personal data on the cloud path and turn the canonical `TaxReturn` into filing-ready outputs. A PII-redacting `IChatClient` middleware pseudonymizes sensitive data before it leaves and rehydrates it on the way back; three exporters (Summary → PDF → XML) project the return, with the XML validated against the portal schema.

**Architecture:** A new `TaxClaw.Privacy` library wraps any `IChatClient` in a `PiiRedactingChatClient` (a `DelegatingChatClient`) driven by an `IPiiDetector` and a per-call `PseudonymMap`; local providers bypass it. A new `TaxClaw.Export` library holds three exporters over the canonical model: `SummaryExporter` (markdown with traces + § citations), `PdfExporter` (form 25 5405 via QuestPDF), and `XmlExporter` (EPO XML + XSD validation). Each exporter is an independent milestone behind an `IReturnExporter` seam.

**Tech Stack:** .NET 10, `Microsoft.Extensions.AI`, `QuestPDF`, `System.Xml`/`System.Xml.Schema`, xUnit. Builds on Plan 1 (`IChatClient` seam) and Plan 2 (`TaxReturn`, `CalculationTrace`).

---

## File Structure

- `src/TaxClaw.Privacy/IPiiDetector.cs`, `RegexPiiDetector.cs` — detect PII spans.
- `src/TaxClaw.Privacy/PseudonymMap.cs` — bidirectional token↔value map.
- `src/TaxClaw.Privacy/PiiRedactingChatClient.cs` — redact outbound, rehydrate inbound.
- `src/TaxClaw.Export/IReturnExporter.cs` — common exporter seam.
- `src/TaxClaw.Export/SummaryExporter.cs` — markdown summary with traces.
- `src/TaxClaw.Export/PdfExporter.cs` — fill form 25 5405 to PDF.
- `src/TaxClaw.Export/Xml/XmlExporter.cs`, `Xml/XsdValidator.cs` — EPO XML + validation.
- Tests under `tests/TaxClaw.Privacy.Tests/` and `tests/TaxClaw.Export.Tests/`.

---

### Task 1: Scaffold privacy and export libraries

**Files:**
- Create: `src/TaxClaw.Privacy`, `src/TaxClaw.Export`, test projects

- [ ] **Step 1: Create and reference projects**

```bash
dotnet new classlib -o src/TaxClaw.Privacy
dotnet new classlib -o src/TaxClaw.Export
dotnet new xunit    -o tests/TaxClaw.Privacy.Tests
dotnet new xunit    -o tests/TaxClaw.Export.Tests
rm src/TaxClaw.Privacy/Class1.cs src/TaxClaw.Export/Class1.cs tests/TaxClaw.Privacy.Tests/UnitTest1.cs tests/TaxClaw.Export.Tests/UnitTest1.cs

dotnet sln add src/TaxClaw.Privacy src/TaxClaw.Export tests/TaxClaw.Privacy.Tests tests/TaxClaw.Export.Tests

dotnet add src/TaxClaw.Privacy package Microsoft.Extensions.AI
dotnet add src/TaxClaw.Export reference src/TaxClaw.Core
dotnet add src/TaxClaw.Export package QuestPDF

dotnet add tests/TaxClaw.Privacy.Tests reference src/TaxClaw.Privacy
dotnet add tests/TaxClaw.Export.Tests reference src/TaxClaw.Core src/TaxClaw.Export
```

- [ ] **Step 2: Verify build, then commit**

Run: `dotnet build`
Expected: `Build succeeded.`

```bash
git add -A
git commit -m "chore(privacy,export): scaffold privacy and export libraries"
```

---

### Task 2: PII detector

**Files:**
- Create: `src/TaxClaw.Privacy/IPiiDetector.cs`
- Create: `src/TaxClaw.Privacy/RegexPiiDetector.cs`
- Test: `tests/TaxClaw.Privacy.Tests/RegexPiiDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Privacy.Tests/RegexPiiDetectorTests.cs`:

```csharp
using TaxClaw.Privacy;
using Xunit;

namespace TaxClaw.Privacy.Tests;

public class RegexPiiDetectorTests
{
    private readonly RegexPiiDetector _detector = new();

    [Fact]
    public void Detects_rodne_cislo()
    {
        var spans = _detector.Detect("My rodné číslo is 900101/1234 today.");
        Assert.Contains(spans, s => s.Kind == "rodne_cislo" && s.Value == "900101/1234");
    }

    [Fact]
    public void Detects_czech_iban()
    {
        var spans = _detector.Detect("Refund to CZ6508000000192000145399 please.");
        Assert.Contains(spans, s => s.Kind == "iban");
    }

    [Fact]
    public void Returns_nothing_for_clean_text()
    {
        Assert.Empty(_detector.Detect("How are RSUs taxed in Czechia?"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Privacy.Tests`
Expected: FAIL — detector types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Privacy/IPiiDetector.cs`:

```csharp
namespace TaxClaw.Privacy;

/// <summary>A detected PII occurrence: its kind and exact substring.</summary>
public readonly record struct PiiSpan(string Kind, string Value);

/// <summary>Finds PII substrings in text so they can be pseudonymized before leaving the machine.</summary>
public interface IPiiDetector
{
    IReadOnlyList<PiiSpan> Detect(string text);
}
```

Create `src/TaxClaw.Privacy/RegexPiiDetector.cs`:

```csharp
using System.Text.RegularExpressions;

namespace TaxClaw.Privacy;

/// <summary>Regex-based detector for Czech PII patterns (rodné číslo, IBAN). Extensible by pattern.</summary>
public sealed partial class RegexPiiDetector : IPiiDetector
{
    [GeneratedRegex(@"\b\d{6}/\d{3,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RodneCislo();

    [GeneratedRegex(@"\bCZ\d{20,22}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CzechIban();

    public IReadOnlyList<PiiSpan> Detect(string text)
    {
        var spans = new List<PiiSpan>();
        foreach (Match m in RodneCislo().Matches(text))
        {
            spans.Add(new PiiSpan("rodne_cislo", m.Value));
        }
        foreach (Match m in CzechIban().Matches(text))
        {
            spans.Add(new PiiSpan("iban", m.Value));
        }
        return spans;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Privacy.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Privacy/IPiiDetector.cs src/TaxClaw.Privacy/RegexPiiDetector.cs tests/TaxClaw.Privacy.Tests/RegexPiiDetectorTests.cs
git commit -m "feat(privacy): add regex PII detector"
```

---

### Task 3: Pseudonym map

**Files:**
- Create: `src/TaxClaw.Privacy/PseudonymMap.cs`
- Test: `tests/TaxClaw.Privacy.Tests/PseudonymMapTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Privacy.Tests/PseudonymMapTests.cs`:

```csharp
using TaxClaw.Privacy;
using Xunit;

namespace TaxClaw.Privacy.Tests;

public class PseudonymMapTests
{
    [Fact]
    public void Same_value_always_maps_to_the_same_token()
    {
        var map = new PseudonymMap();
        string t1 = map.Tokenize("rodne_cislo", "900101/1234");
        string t2 = map.Tokenize("rodne_cislo", "900101/1234");
        Assert.Equal(t1, t2);
    }

    [Fact]
    public void Redact_then_restore_is_round_trip_safe()
    {
        var map = new PseudonymMap();
        string token = map.Tokenize("iban", "CZ6508000000192000145399");

        string outbound = $"Send refund to {token}.";
        string restored = map.Restore(outbound);

        Assert.Equal("Send refund to CZ6508000000192000145399.", restored);
    }

    [Fact]
    public void Tokens_do_not_resemble_the_original_value()
    {
        var map = new PseudonymMap();
        string token = map.Tokenize("rodne_cislo", "900101/1234");
        Assert.DoesNotContain("900101", token);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Privacy.Tests`
Expected: FAIL — `PseudonymMap` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Privacy/PseudonymMap.cs`:

```csharp
namespace TaxClaw.Privacy;

/// <summary>
/// A per-conversation bidirectional map between PII values and opaque placeholder tokens. Redaction
/// replaces values with tokens before a cloud call; restore swaps them back in the response.
/// </summary>
public sealed class PseudonymMap
{
    private readonly Dictionary<string, string> _valueToToken = new();
    private readonly Dictionary<string, string> _tokenToValue = new();
    private int _counter;

    public string Tokenize(string kind, string value)
    {
        if (_valueToToken.TryGetValue(value, out string? existing))
        {
            return existing;
        }

        string token = $"[[{kind.ToUpperInvariant()}_{++_counter}]]";
        _valueToToken[value] = token;
        _tokenToValue[token] = value;
        return token;
    }

    public string Restore(string text)
    {
        string result = text;
        foreach ((string token, string value) in _tokenToValue)
        {
            result = result.Replace(token, value);
        }
        return result;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Privacy.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Privacy/PseudonymMap.cs tests/TaxClaw.Privacy.Tests/PseudonymMapTests.cs
git commit -m "feat(privacy): add pseudonym map"
```

---

### Task 4: PII-redacting chat client middleware

**Files:**
- Create: `src/TaxClaw.Privacy/PiiRedactingChatClient.cs`
- Test: `tests/TaxClaw.Privacy.Tests/PiiRedactingChatClientTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Privacy.Tests/PiiRedactingChatClientTests.cs`:

```csharp
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Privacy;
using Xunit;

namespace TaxClaw.Privacy.Tests;

public class PiiRedactingChatClientTests
{
    [Fact]
    public async Task Outbound_pii_is_replaced_by_a_token_before_the_inner_client()
    {
        var inner = new CapturingChatClient("ok");
        IChatClient client = new PiiRedactingChatClient(inner, new RegexPiiDetector());

        await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.User, "My rodné číslo is 900101/1234.")
        });

        string sent = inner.LastUserText!;
        Assert.DoesNotContain("900101/1234", sent);
        Assert.Contains("[[RODNE_CISLO_1]]", sent);
    }

    [Fact]
    public async Task Inbound_tokens_are_restored_in_the_response()
    {
        // Inner echoes back the token, which must be rehydrated to the real value for the user.
        var inner = new EchoTokenChatClient();
        IChatClient client = new PiiRedactingChatClient(inner, new RegexPiiDetector());

        ChatResponse response = await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.User, "Account CZ6508000000192000145399 ok?")
        });

        Assert.Contains("CZ6508000000192000145399", response.Text);
    }

    private sealed class CapturingChatClient(string reply) : IChatClient
    {
        public string? LastUserText { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastUserText = messages.Last(m => m.Role == ChatRole.User).Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastUserText = messages.Last(m => m.Role == ChatRole.User).Text;
            yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class EchoTokenChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            string text = messages.Last(m => m.Role == ChatRole.User).Text;
            string token = text.Contains("[[IBAN_1]]") ? "[[IBAN_1]]" : "(none)";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Confirmed {token}")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Confirmed [[IBAN_1]]");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Privacy.Tests`
Expected: FAIL — `PiiRedactingChatClient` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Privacy/PiiRedactingChatClient.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace TaxClaw.Privacy;

/// <summary>
/// A chat-client middleware that pseudonymizes PII in outgoing messages and restores it in the
/// response. Wrap a cloud provider's client with this; for a local provider, skip it entirely.
/// Each call uses a fresh <see cref="PseudonymMap"/> so tokens never collide across conversations.
/// </summary>
public sealed class PiiRedactingChatClient(IChatClient inner, IPiiDetector detector) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var map = new PseudonymMap();
        var redacted = messages.Select(m => Redact(m, map)).ToList();

        ChatResponse response = await inner.GetResponseAsync(redacted, options, cancellationToken);

        foreach (ChatMessage message in response.Messages)
        {
            RestoreInPlace(message, map);
        }
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var map = new PseudonymMap();
        var redacted = messages.Select(m => Redact(m, map)).ToList();

        await foreach (ChatResponseUpdate update in inner.GetStreamingResponseAsync(redacted, options, cancellationToken))
        {
            yield return new ChatResponseUpdate(update.Role ?? ChatRole.Assistant, map.Restore(update.Text));
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();

    private ChatMessage Redact(ChatMessage message, PseudonymMap map)
    {
        string redactedText = message.Text;
        foreach (PiiSpan span in detector.Detect(message.Text))
        {
            redactedText = redactedText.Replace(span.Value, map.Tokenize(span.Kind, span.Value));
        }
        return new ChatMessage(message.Role, redactedText);
    }

    private static void RestoreInPlace(ChatMessage message, PseudonymMap map)
    {
        for (int i = 0; i < message.Contents.Count; i++)
        {
            if (message.Contents[i] is TextContent text)
            {
                message.Contents[i] = new TextContent(map.Restore(text.Text));
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Privacy.Tests`
Expected: PASS — outbound value tokenized; inbound token restored.

> If `ChatMessage.Text` is read-only on your package version, the `Redact` helper already builds a
> new message from `message.Role` + redacted text, so no setter is needed. `RestoreInPlace` mutates
> the `Contents` list, which is the supported way to rewrite message text.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Privacy/PiiRedactingChatClient.cs tests/TaxClaw.Privacy.Tests/PiiRedactingChatClientTests.cs
git commit -m "feat(privacy): add PII-redacting chat client middleware"
```

---

### Task 5: Exporter seam + Summary exporter (milestone 1)

**Files:**
- Create: `src/TaxClaw.Export/IReturnExporter.cs`
- Create: `src/TaxClaw.Export/SummaryExporter.cs`
- Test: `tests/TaxClaw.Export.Tests/SummaryExporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Export.Tests/SummaryExporterTests.cs`:

```csharp
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using TaxClaw.Export;
using Xunit;

namespace TaxClaw.Export.Tests;

public class SummaryExporterTests
{
    private static TaxReturn SampleReturn()
    {
        var trace = new CalculationTrace("r38",
            new[]
            {
                new CalculationStep("read r36", "120000"),
                new CalculationStep("subtract r37", "120000 - 20000 = 100000")
            },
            "100000",
            new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));

        return new TaxReturn(TaxYear.Of(2027)).WithLine("r38", 100000m, trace);
    }

    [Fact]
    public void Summary_includes_line_value_steps_and_citation()
    {
        string md = new SummaryExporter().Export(SampleReturn());

        Assert.Contains("r38", md);
        Assert.Contains("100000", md);
        Assert.Contains("subtract r37", md);
        Assert.Contains("§ 16", md);
    }

    [Fact]
    public void Summary_states_it_is_not_tax_advice()
    {
        string md = new SummaryExporter().Export(SampleReturn());
        Assert.Contains("not tax advice", md, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Export.Tests`
Expected: FAIL — exporter types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Export/IReturnExporter.cs`:

```csharp
using TaxClaw.Core.Model;

namespace TaxClaw.Export;

/// <summary>Projects the canonical return into an output format. Each format is one implementation.</summary>
public interface IReturnExporter<out T>
{
    T Export(TaxReturn taxReturn);
}
```

Create `src/TaxClaw.Export/SummaryExporter.cs`:

```csharp
using System.Text;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;

namespace TaxClaw.Export;

/// <summary>
/// Renders the return as a human-readable markdown summary: every populated line with its value,
/// the calculation steps, and the legislation citation. This is export milestone 1.
/// </summary>
public sealed class SummaryExporter : IReturnExporter<string>
{
    public string Export(TaxReturn taxReturn)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Tax declaration summary — {taxReturn.Year}");
        sb.AppendLine();
        sb.AppendLine("> This is a computed draft and is **not tax advice**. Review every figure before filing.");
        sb.AppendLine();

        foreach ((string lineId, decimal value) in taxReturn.Lines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"## Line {lineId}: {value}");
            CalculationTrace? trace = taxReturn.GetTrace(lineId);
            if (trace is not null)
            {
                foreach (CalculationStep step in trace.Steps)
                {
                    sb.AppendLine($"- {step.Description}: {step.Detail}");
                }
                sb.AppendLine($"- Source: {trace.Provenance}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Export.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Export/IReturnExporter.cs src/TaxClaw.Export/SummaryExporter.cs tests/TaxClaw.Export.Tests/SummaryExporterTests.cs
git commit -m "feat(export): add summary exporter (milestone 1)"
```

---

### Task 6: PDF exporter (milestone 2)

**Files:**
- Create: `src/TaxClaw.Export/PdfExporter.cs`
- Test: `tests/TaxClaw.Export.Tests/PdfExporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Export.Tests/PdfExporterTests.cs`:

```csharp
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using TaxClaw.Export;
using Xunit;

namespace TaxClaw.Export.Tests;

public class PdfExporterTests
{
    private static TaxReturn SampleReturn()
    {
        var trace = new CalculationTrace("r38", new[] { new CalculationStep("x", "y") }, "100000",
            new Provenance(FormLine: "r38", Version: "2027.1"));
        return new TaxReturn(TaxYear.Of(2027)).WithLine("r38", 100000m, trace);
    }

    [Fact]
    public void Produces_a_non_empty_pdf_with_a_pdf_header()
    {
        byte[] pdf = new PdfExporter().Export(SampleReturn());

        Assert.True(pdf.Length > 1000);
        // PDF files start with "%PDF-".
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Export.Tests`
Expected: FAIL — `PdfExporter` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Export/PdfExporter.cs`:

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxClaw.Core.Model;

namespace TaxClaw.Export;

/// <summary>
/// Renders the return as a PDF approximating form 25 5405 (one row per populated line). This is
/// export milestone 2. The exact official layout is refined against the downloaded form template.
/// </summary>
public sealed class PdfExporter : IReturnExporter<byte[]>
{
    static PdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Export(TaxReturn taxReturn)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Text($"Přiznání k dani z příjmů FO — {taxReturn.Year}").Bold().FontSize(14);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Řádek").Bold();
                        header.Cell().Text("Částka (Kč)").Bold();
                    });

                    foreach ((string lineId, decimal value) in taxReturn.Lines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    {
                        table.Cell().Text(lineId);
                        table.Cell().Text(value.ToString());
                    }
                });

                page.Footer().Text("Computed draft — not tax advice.").FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Export.Tests`
Expected: PASS — a `%PDF` document of non-trivial size is produced.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Export/PdfExporter.cs tests/TaxClaw.Export.Tests/PdfExporterTests.cs
git commit -m "feat(export): add PDF exporter (milestone 2)"
```

---

### Task 7: XML exporter + XSD validation (milestone 3)

**Files:**
- Create: `src/TaxClaw.Export/Xml/XmlExporter.cs`
- Create: `src/TaxClaw.Export/Xml/XsdValidator.cs`
- Test: `tests/TaxClaw.Export.Tests/XmlExporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Export.Tests/XmlExporterTests.cs`:

```csharp
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using TaxClaw.Export.Xml;
using Xunit;

namespace TaxClaw.Export.Tests;

public class XmlExporterTests
{
    private static TaxReturn SampleReturn()
    {
        var trace = new CalculationTrace("r38", new[] { new CalculationStep("x", "y") }, "100000",
            new Provenance(FormLine: "r38", Version: "2027.1"));
        return new TaxReturn(TaxYear.Of(2027)).WithLine("r38", 100000m, trace);
    }

    private const string Xsd =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="Declaration">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="Line" maxOccurs="unbounded">
                  <xs:complexType>
                    <xs:attribute name="id" type="xs:string" use="required"/>
                    <xs:attribute name="value" type="xs:decimal" use="required"/>
                  </xs:complexType>
                </xs:element>
              </xs:sequence>
              <xs:attribute name="year" type="xs:int" use="required"/>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    [Fact]
    public void Generated_xml_contains_the_year_and_lines()
    {
        string xml = new XmlExporter().Export(SampleReturn());
        Assert.Contains("year=\"2027\"", xml);
        Assert.Contains("id=\"r38\"", xml);
        Assert.Contains("value=\"100000\"", xml);
    }

    [Fact]
    public void Generated_xml_validates_against_the_schema()
    {
        string xml = new XmlExporter().Export(SampleReturn());
        var report = XsdValidator.Validate(xml, Xsd);
        Assert.True(report.IsValid, string.Join("; ", report.Errors));
    }

    [Fact]
    public void Malformed_xml_fails_validation()
    {
        const string bad = "<Declaration year=\"2027\"><Line id=\"r38\"/></Declaration>"; // missing value attr
        var report = XsdValidator.Validate(bad, Xsd);
        Assert.False(report.IsValid);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Export.Tests`
Expected: FAIL — XML types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Export/Xml/XmlExporter.cs`:

```csharp
using System.Globalization;
using System.Xml.Linq;
using TaxClaw.Core.Model;

namespace TaxClaw.Export.Xml;

/// <summary>
/// Projects the return to the portal XML shape (one element per populated line). This is export
/// milestone 3; the element/attribute names track the EPO schema for form 25 5405 once obtained.
/// </summary>
public sealed class XmlExporter : IReturnExporter<string>
{
    public string Export(TaxReturn taxReturn)
    {
        var doc = new XDocument(
            new XElement("Declaration",
                new XAttribute("year", taxReturn.Year.Year),
                taxReturn.Lines
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => new XElement("Line",
                        new XAttribute("id", kv.Key),
                        new XAttribute("value", kv.Value.ToString(CultureInfo.InvariantCulture))))));

        return doc.Declaration is null
            ? doc.ToString()
            : doc.Declaration + Environment.NewLine + doc;
    }
}
```

Create `src/TaxClaw.Export/Xml/XsdValidator.cs`:

```csharp
using System.Xml;
using System.Xml.Schema;

namespace TaxClaw.Export.Xml;

/// <summary>The result of validating XML against a schema.</summary>
public sealed record XsdReport(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Validates exported XML against the portal XSD before submission.</summary>
public static class XsdValidator
{
    public static XsdReport Validate(string xml, string xsd)
    {
        var errors = new List<string>();

        var schemas = new XmlSchemaSet();
        using (var schemaReader = XmlReader.Create(new StringReader(xsd)))
        {
            schemas.Add(null, schemaReader);
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas
        };
        settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            errors.Add(ex.Message);
        }

        return new XsdReport(errors.Count == 0, errors);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Export.Tests`
Expected: PASS — XML contains year/lines, validates against the schema, malformed XML fails.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 6: Commit**

```bash
git add src/TaxClaw.Export/Xml tests/TaxClaw.Export.Tests/XmlExporterTests.cs
git commit -m "feat(export): add XML exporter with XSD validation (milestone 3)"
```

---

### Task 8: Wire privacy middleware into the provider factory

**Files:**
- Modify: `src/TaxClaw.Llm/ChatClientFactory.cs` (wrap cloud providers; skip local)
- Modify: `src/TaxClaw.Llm/LlmOptions.cs` (add `RedactPii` flag)
- Add reference: `src/TaxClaw.Llm` → `src/TaxClaw.Privacy`
- Test: `tests/TaxClaw.Llm.Tests/RedactionWiringTests.cs`

- [ ] **Step 1: Add the reference**

```bash
dotnet add src/TaxClaw.Llm reference src/TaxClaw.Privacy
dotnet add tests/TaxClaw.Llm.Tests reference src/TaxClaw.Privacy
```

- [ ] **Step 2: Write the failing test**

Create `tests/TaxClaw.Llm.Tests/RedactionWiringTests.cs`:

```csharp
using TaxClaw.Llm;
using TaxClaw.Privacy;
using Xunit;

namespace TaxClaw.Llm.Tests;

public class RedactionWiringTests
{
    [Fact]
    public void Ollama_is_not_wrapped_in_redaction()
    {
        var options = new LlmOptions { Provider = "ollama", Model = "llama3.1", RedactPii = true };
        var client = new ChatClientFactory(options).Create();
        Assert.IsNotType<PiiRedactingChatClient>(client);
    }

    [Fact]
    public void Cloud_provider_is_wrapped_when_redaction_is_enabled()
    {
        var options = new LlmOptions
        {
            Provider = "openai", Model = "gpt-4o", ApiKey = "k", RedactPii = true
        };
        var client = new ChatClientFactory(options).Create();
        Assert.IsType<PiiRedactingChatClient>(client);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Llm.Tests`
Expected: FAIL — `LlmOptions.RedactPii` does not exist / cloud client is not wrapped.

- [ ] **Step 4: Update options and factory**

In `src/TaxClaw.Llm/LlmOptions.cs`, add the flag inside the `LlmOptions` class (after `ApiKey`):

```csharp
    /// <summary>When true, wrap cloud providers with PII redaction. Ignored for local providers.</summary>
    public bool RedactPii { get; set; } = true;
```

In `src/TaxClaw.Llm/ChatClientFactory.cs`, change `Create()` to wrap cloud providers. Replace the method body:

```csharp
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
```

with:

```csharp
    public IChatClient Create()
    {
        string provider = options.Provider.ToLowerInvariant();

        IChatClient baseClient = provider switch
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

        bool isLocal = provider == "ollama";
        return options.RedactPii && !isLocal
            ? new PiiRedactingChatClient(baseClient, new RegexPiiDetector())
            : baseClient;
    }
```

Add the using to the top of `ChatClientFactory.cs`:

```csharp
using TaxClaw.Privacy;
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Llm.Tests`
Expected: PASS — local not wrapped; cloud wrapped. Existing `ChatClientFactoryTests` still pass (the unknown/validation tests are unaffected; the Ollama test now returns either the bare client, still non-null).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(privacy): wrap cloud providers with PII redaction in the factory"
```

---

## Self-Review

**1. Spec coverage:**
- PII boundary as chat-client middleware (pseudonymize → call → rehydrate) → Tasks 2, 3, 4. ✓
- Local mode bypasses redaction → Task 8. ✓
- Exporters over the canonical model, incremental Summary → PDF → XML → Tasks 5, 6, 7. ✓
- Summary carries traces + § citations; disclaimer "not tax advice" → Task 5. ✓
- PDF approximates form 25 5405 → Task 6. ✓
- XML for the portal validated against XSD → Task 7. ✓
- Wired into the provider factory (the same `IChatClient` seam from Plan 1) → Task 8. ✓
- *Note (at-rest encryption):* the spec's OS-keychain at-rest encryption is a small, self-contained follow-up on the storage layer (an `IDataProtector` seam around `JsonProfileStore`/`JsonProjectStore`); it is independent of this plan's cloud-path protection and can be a short addendum. Flagged so it is not lost.

**2. Placeholder scan:** No TBD/TODO. Every code step complete; tests assert real behavior (PDF magic bytes, XSD pass/fail, token round-trip). Conditional notes (`ChatMessage.Text` read-only; official XSD/form layout) are concrete guidance, not placeholders. ✓

**3. Type consistency:** `IPiiDetector.Detect(text) -> IReadOnlyList<PiiSpan>` and `PiiSpan(Kind, Value)` consistent (2, 4, 8). `PseudonymMap.Tokenize(kind, value)` / `Restore(text)` consistent (3, 4). `PiiRedactingChatClient(inner, detector)` consistent (4, 8). `IReturnExporter<T>.Export(TaxReturn)` consistent (5, 6, 7). `TaxReturn.Lines` / `GetTrace` / `Year.Year` match Plan 2 (5, 6, 7). `CalculationTrace`/`CalculationStep`/`Provenance` match Plan 2 (5, 6, 7). `LlmOptions.RedactPii` added in Task 8 and used in the factory; existing `LlmOptions` fields from Plan 1 unchanged. ✓
