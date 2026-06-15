# tax-claw Document Pipeline — Implementation Plan (Plan 4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user drop a document into the chat and have the system classify its type, extract text (with an OCR/Vision path for scans), pull structured entities against a per-type schema, and map them into the canonical `TaxReturn` — all treating document content as untrusted data, with provenance on every extracted value.

**Architecture:** A new `TaxClaw.Documents` library. Ingestion detects whether a file has a text layer (`ITextExtractor`) or needs recognition (`IRecognizer`, with a fake in tests). Classification (`IDocumentClassifier`) maps content to a `DocumentType`. Each type owns an `EntitySchema`; an `IEntityExtractor` returns schema-bound `ExtractionResult`s (required fields enforced). `DocumentMapper` turns validated entities into `IncomeItem`s on the return. A `DocumentPipeline` orchestrates the stages. Extraction is schema-constrained so document text can never act as instructions.

**Tech Stack:** .NET 10, `Microsoft.Extensions.AI`, xUnit. Builds on Plan 1 (`TaxClaw.Core`) and Plan 2 (`TaxReturn`, `Money`, `IncomeItem`).

---

## File Structure

- `src/TaxClaw.Documents/Model/DocumentType.cs` — enum of supported types.
- `src/TaxClaw.Documents/Model/SourceDocument.cs` — bytes, filename, media kind.
- `src/TaxClaw.Documents/Model/ExtractedText.cs` — text + whether OCR was used + page map.
- `src/TaxClaw.Documents/Extract/ITextExtractor.cs`, `Extract/IRecognizer.cs`, `Extract/TextLayerDetector.cs`.
- `src/TaxClaw.Documents/Classify/IDocumentClassifier.cs`, `Classify/KeywordClassifier.cs`.
- `src/TaxClaw.Documents/Entities/EntitySchema.cs`, `Entities/ExtractionResult.cs`, `Entities/IEntityExtractor.cs`, `Entities/SchemaValidator.cs`, `Entities/DocumentSchemas.cs`.
- `src/TaxClaw.Documents/Map/DocumentMapper.cs` — entities → `IncomeItem`.
- `src/TaxClaw.Documents/DocumentPipeline.cs` — orchestrates the stages.
- Tests under `tests/TaxClaw.Documents.Tests/`.

---

### Task 1: Scaffold the documents library

**Files:**
- Create: `src/TaxClaw.Documents`, `tests/TaxClaw.Documents.Tests`

- [ ] **Step 1: Create and reference projects**

```bash
dotnet new classlib -o src/TaxClaw.Documents
dotnet new xunit    -o tests/TaxClaw.Documents.Tests
rm src/TaxClaw.Documents/Class1.cs tests/TaxClaw.Documents.Tests/UnitTest1.cs

dotnet sln add src/TaxClaw.Documents tests/TaxClaw.Documents.Tests
dotnet add src/TaxClaw.Documents reference src/TaxClaw.Core
dotnet add src/TaxClaw.Documents package Microsoft.Extensions.AI
dotnet add tests/TaxClaw.Documents.Tests reference src/TaxClaw.Core src/TaxClaw.Documents
```

- [ ] **Step 2: Verify build, then commit**

Run: `dotnet build`
Expected: `Build succeeded.`

```bash
git add -A
git commit -m "chore(documents): scaffold documents library"
```

---

### Task 2: Document models

**Files:**
- Create: `src/TaxClaw.Documents/Model/DocumentType.cs`
- Create: `src/TaxClaw.Documents/Model/SourceDocument.cs`
- Create: `src/TaxClaw.Documents/Model/ExtractedText.cs`
- Test: `tests/TaxClaw.Documents.Tests/SourceDocumentTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/SourceDocumentTests.cs`:

```csharp
using TaxClaw.Documents.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class SourceDocumentTests
{
    [Theory]
    [InlineData("statement.pdf", MediaKind.Pdf)]
    [InlineData("scan.JPG", MediaKind.Image)]
    [InlineData("photo.heic", MediaKind.Image)]
    [InlineData("export.csv", MediaKind.Tabular)]
    [InlineData("notes.txt", MediaKind.Text)]
    public void Media_kind_is_inferred_from_extension(string name, MediaKind expected)
    {
        var doc = SourceDocument.FromBytes(name, new byte[] { 1, 2, 3 });
        Assert.Equal(expected, doc.Kind);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — model types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/Model/DocumentType.cs`:

```csharp
namespace TaxClaw.Documents.Model;

/// <summary>The recognized kind of tax document (drives which entity schema is used).</summary>
public enum DocumentType
{
    Unknown,
    EmploymentIncomeStatement, // potvrzení o zdanitelných příjmech
    RsuVestingStatement,
    DividendStatement,
    BrokerageTradeConfirmation
}
```

Create `src/TaxClaw.Documents/Model/SourceDocument.cs`:

```csharp
namespace TaxClaw.Documents.Model;

/// <summary>Broad category of file content, used to choose the extraction path.</summary>
public enum MediaKind { Pdf, Image, Tabular, Text, Unknown }

/// <summary>A raw document handed to the pipeline. Content is untrusted input.</summary>
public sealed record SourceDocument(string FileName, byte[] Bytes, MediaKind Kind)
{
    public static SourceDocument FromBytes(string fileName, byte[] bytes) =>
        new(fileName, bytes, InferKind(fileName));

    private static MediaKind InferKind(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => MediaKind.Pdf,
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".heif" or ".tif" or ".tiff" => MediaKind.Image,
            ".csv" or ".xlsx" or ".xls" => MediaKind.Tabular,
            ".txt" => MediaKind.Text,
            _ => MediaKind.Unknown
        };
}
```

Create `src/TaxClaw.Documents/Model/ExtractedText.cs`:

```csharp
namespace TaxClaw.Documents.Model;

/// <summary>Text recovered from a document, noting whether recognition (OCR/Vision) was needed.</summary>
public sealed record ExtractedText(string Text, bool UsedRecognition, int PageCount = 1);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Documents/Model tests/TaxClaw.Documents.Tests/SourceDocumentTests.cs
git commit -m "feat(documents): add source document and text models"
```

---

### Task 3: Text-layer detection and extraction seam

**Files:**
- Create: `src/TaxClaw.Documents/Extract/ITextExtractor.cs`
- Create: `src/TaxClaw.Documents/Extract/IRecognizer.cs`
- Create: `src/TaxClaw.Documents/Extract/TextLayerDetector.cs`
- Test: `tests/TaxClaw.Documents.Tests/TextLayerDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/TextLayerDetectorTests.cs`:

```csharp
using System.Text;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class TextLayerDetectorTests
{
    private sealed class PlainTextExtractor : ITextExtractor
    {
        public Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default)
        {
            string text = Encoding.UTF8.GetString(doc.Bytes);
            return Task.FromResult<ExtractedText?>(
                text.Trim().Length > 0 ? new ExtractedText(text, UsedRecognition: false) : null);
        }
    }

    private sealed class StubRecognizer : IRecognizer
    {
        public Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default) =>
            Task.FromResult(new ExtractedText("recognized", UsedRecognition: true));
    }

    [Fact]
    public async Task Uses_text_layer_when_present()
    {
        var detector = new TextLayerDetector(new PlainTextExtractor(), new StubRecognizer());
        var doc = SourceDocument.FromBytes("a.txt", Encoding.UTF8.GetBytes("hello"));

        var result = await detector.ExtractAsync(doc);

        Assert.False(result.UsedRecognition);
        Assert.Equal("hello", result.Text);
    }

    [Fact]
    public async Task Falls_back_to_recognition_when_no_text_layer()
    {
        var detector = new TextLayerDetector(new PlainTextExtractor(), new StubRecognizer());
        var doc = SourceDocument.FromBytes("scan.png", Array.Empty<byte>());

        var result = await detector.ExtractAsync(doc);

        Assert.True(result.UsedRecognition);
        Assert.Equal("recognized", result.Text);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — extraction types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/Extract/ITextExtractor.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>
/// Tries to pull an existing text layer from a document (e.g. PdfPig for text PDFs). Returns null
/// when there is no usable text, signalling the recognition fallback.
/// </summary>
public interface ITextExtractor
{
    Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default);
}
```

Create `src/TaxClaw.Documents/Extract/IRecognizer.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>
/// Recovers text from scans/photos/image-PDFs via OCR (Tesseract/OS Vision) or a Vision-LLM.
/// Faked in tests; the concrete implementation respects the privacy mode (Plan 7).
/// </summary>
public interface IRecognizer
{
    Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default);
}
```

Create `src/TaxClaw.Documents/Extract/TextLayerDetector.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>Prefers a text layer; falls back to recognition when none is found.</summary>
public sealed class TextLayerDetector(ITextExtractor textExtractor, IRecognizer recognizer)
{
    public async Task<ExtractedText> ExtractAsync(SourceDocument doc, CancellationToken ct = default)
    {
        ExtractedText? direct = await textExtractor.TryExtractAsync(doc, ct);
        return direct ?? await recognizer.RecognizeAsync(doc, ct);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Documents/Extract tests/TaxClaw.Documents.Tests/TextLayerDetectorTests.cs
git commit -m "feat(documents): add text-layer detection with recognition fallback"
```

---

### Task 4: Document classifier

**Files:**
- Create: `src/TaxClaw.Documents/Classify/IDocumentClassifier.cs`
- Create: `src/TaxClaw.Documents/Classify/KeywordClassifier.cs`
- Test: `tests/TaxClaw.Documents.Tests/KeywordClassifierTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/KeywordClassifierTests.cs`:

```csharp
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class KeywordClassifierTests
{
    private readonly KeywordClassifier _classifier = new();

    [Theory]
    [InlineData("RSU vesting: 100 shares vested, FMV 50 USD", DocumentType.RsuVestingStatement)]
    [InlineData("Dividend payment, withholding tax 15%", DocumentType.DividendStatement)]
    [InlineData("Potvrzení o zdanitelných příjmech ze závislé činnosti", DocumentType.EmploymentIncomeStatement)]
    [InlineData("Trade confirmation: SELL 10 shares", DocumentType.BrokerageTradeConfirmation)]
    public void Classifies_by_signal_terms(string text, DocumentType expected)
    {
        var result = _classifier.Classify(new ExtractedText(text, false));
        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public void Unrecognized_text_is_low_confidence_unknown()
    {
        var result = _classifier.Classify(new ExtractedText("grocery receipt for milk", false));
        Assert.Equal(DocumentType.Unknown, result.Type);
        Assert.True(result.Confidence < 0.5);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — classifier types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/Classify/IDocumentClassifier.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Classify;

/// <summary>The classifier's verdict and how confident it is (0..1).</summary>
public readonly record struct Classification(DocumentType Type, double Confidence);

/// <summary>
/// Decides a document's type. The keyword implementation is a cheap first pass; an LLM-backed
/// classifier can implement this same seam for ambiguous documents.
/// </summary>
public interface IDocumentClassifier
{
    Classification Classify(ExtractedText text);
}
```

Create `src/TaxClaw.Documents/Classify/KeywordClassifier.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Classify;

/// <summary>Scores each document type by counting signal terms; ties broken by first definition.</summary>
public sealed class KeywordClassifier : IDocumentClassifier
{
    private static readonly (DocumentType Type, string[] Terms)[] Signals =
    [
        (DocumentType.RsuVestingStatement, ["rsu", "vesting", "vested", "fmv", "restricted stock"]),
        (DocumentType.DividendStatement, ["dividend", "withholding"]),
        (DocumentType.EmploymentIncomeStatement, ["zdanitelných příjmech", "závislé činnosti", "employment income"]),
        (DocumentType.BrokerageTradeConfirmation, ["trade confirmation", "sell", "buy", "settlement"])
    ];

    public Classification Classify(ExtractedText text)
    {
        string haystack = text.Text.ToLowerInvariant();

        DocumentType best = DocumentType.Unknown;
        int bestHits = 0;

        foreach ((DocumentType type, string[] terms) in Signals)
        {
            int hits = terms.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (hits > bestHits)
            {
                bestHits = hits;
                best = type;
            }
        }

        double confidence = bestHits == 0 ? 0.0 : System.Math.Min(1.0, 0.5 + 0.25 * bestHits);
        return new Classification(best, confidence);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Documents/Classify tests/TaxClaw.Documents.Tests/KeywordClassifierTests.cs
git commit -m "feat(documents): add keyword document classifier"
```

---

### Task 5: Entity schema and validation

**Files:**
- Create: `src/TaxClaw.Documents/Entities/EntitySchema.cs`
- Create: `src/TaxClaw.Documents/Entities/ExtractionResult.cs`
- Create: `src/TaxClaw.Documents/Entities/SchemaValidator.cs`
- Create: `src/TaxClaw.Documents/Entities/DocumentSchemas.cs`
- Test: `tests/TaxClaw.Documents.Tests/SchemaValidatorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/SchemaValidatorTests.cs`:

```csharp
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class SchemaValidatorTests
{
    [Fact]
    public void Validation_passes_when_all_required_fields_present()
    {
        EntitySchema schema = DocumentSchemas.For(DocumentType.DividendStatement);
        var result = new ExtractionResult(DocumentType.DividendStatement, new Dictionary<string, string>
        {
            ["issuer"] = "Microsoft",
            ["pay_date"] = "2027-03-10",
            ["gross_amount"] = "100.00",
            ["currency"] = "USD",
            ["withholding_tax"] = "15.00"
        });

        var report = SchemaValidator.Validate(result, schema);

        Assert.True(report.IsValid);
        Assert.Empty(report.MissingFields);
    }

    [Fact]
    public void Validation_reports_missing_required_fields()
    {
        EntitySchema schema = DocumentSchemas.For(DocumentType.DividendStatement);
        var result = new ExtractionResult(DocumentType.DividendStatement, new Dictionary<string, string>
        {
            ["issuer"] = "Microsoft"
        });

        var report = SchemaValidator.Validate(result, schema);

        Assert.False(report.IsValid);
        Assert.Contains("gross_amount", report.MissingFields);
        Assert.Contains("currency", report.MissingFields);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — entity types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/Entities/EntitySchema.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>A single field the pipeline expects to extract from a document.</summary>
public sealed record EntityField(string Name, bool Required, string Description);

/// <summary>
/// The set of fields to extract for a document type. Extraction is bound to this schema, which is
/// what stops free-form document text from being treated as instructions.
/// </summary>
public sealed record EntitySchema(DocumentType Type, IReadOnlyList<EntityField> Fields)
{
    public IEnumerable<EntityField> RequiredFields => Fields.Where(f => f.Required);
}
```

Create `src/TaxClaw.Documents/Entities/ExtractionResult.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>Extracted field values for a document, keyed by schema field name.</summary>
public sealed record ExtractionResult(DocumentType Type, IReadOnlyDictionary<string, string> Fields)
{
    public string? Get(string field) => Fields.TryGetValue(field, out var v) ? v : null;
}

/// <summary>Outcome of validating an extraction against its schema.</summary>
public sealed record ValidationReport(bool IsValid, IReadOnlyList<string> MissingFields);
```

Create `src/TaxClaw.Documents/Entities/SchemaValidator.cs`:

```csharp
namespace TaxClaw.Documents.Entities;

/// <summary>Checks that every required schema field is present and non-blank.</summary>
public static class SchemaValidator
{
    public static ValidationReport Validate(ExtractionResult result, EntitySchema schema)
    {
        var missing = schema.RequiredFields
            .Where(f => string.IsNullOrWhiteSpace(result.Get(f.Name)))
            .Select(f => f.Name)
            .ToList();

        return new ValidationReport(missing.Count == 0, missing);
    }
}
```

Create `src/TaxClaw.Documents/Entities/DocumentSchemas.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>The per-document-type extraction schemas (what entities to pull).</summary>
public static class DocumentSchemas
{
    public static EntitySchema For(DocumentType type) => type switch
    {
        DocumentType.RsuVestingStatement => new(type,
        [
            new("vest_date", true, "Date shares vested (yyyy-MM-dd)"),
            new("shares", true, "Number of shares vested"),
            new("fmv_per_share", true, "Fair market value per share at vest"),
            new("currency", true, "Currency of FMV, e.g. USD"),
            new("tax_withheld", false, "Tax already withheld, if any")
        ]),
        DocumentType.DividendStatement => new(type,
        [
            new("issuer", true, "Dividend-paying entity"),
            new("pay_date", true, "Payment date (yyyy-MM-dd)"),
            new("gross_amount", true, "Gross dividend amount"),
            new("currency", true, "Currency, e.g. USD"),
            new("withholding_tax", true, "Tax withheld at source")
        ]),
        DocumentType.EmploymentIncomeStatement => new(type,
        [
            new("gross_income", true, "Gross employment income"),
            new("tax_advances", true, "Income tax advances withheld"),
            new("currency", true, "Currency, e.g. CZK")
        ]),
        DocumentType.BrokerageTradeConfirmation => new(type,
        [
            new("trade_date", true, "Trade date (yyyy-MM-dd)"),
            new("side", true, "BUY or SELL"),
            new("shares", true, "Number of shares"),
            new("price_per_share", true, "Price per share"),
            new("currency", true, "Currency, e.g. USD")
        ]),
        _ => new(DocumentType.Unknown, [])
    };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Documents/Entities tests/TaxClaw.Documents.Tests/SchemaValidatorTests.cs
git commit -m "feat(documents): add entity schemas and validation"
```

---

### Task 6: Entity extractor seam + deterministic line-based extractor

**Files:**
- Create: `src/TaxClaw.Documents/Entities/IEntityExtractor.cs`
- Create: `src/TaxClaw.Documents/Entities/LabelledLineExtractor.cs`
- Test: `tests/TaxClaw.Documents.Tests/LabelledLineExtractorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/LabelledLineExtractorTests.cs`:

```csharp
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class LabelledLineExtractorTests
{
    [Fact]
    public async Task Extracts_fields_from_label_colon_value_lines()
    {
        const string text =
            "issuer: Microsoft\n" +
            "pay_date: 2027-03-10\n" +
            "gross_amount: 100.00\n" +
            "currency: USD\n" +
            "withholding_tax: 15.00";

        var schema = DocumentSchemas.For(DocumentType.DividendStatement);
        var extractor = new LabelledLineExtractor();

        ExtractionResult result = await extractor.ExtractAsync(
            new ExtractedText(text, false), schema);

        Assert.Equal("Microsoft", result.Get("issuer"));
        Assert.Equal("USD", result.Get("currency"));
    }

    [Fact]
    public async Task Ignores_lines_that_are_not_schema_fields()
    {
        const string text = "ignore me: hacker instructions\nissuer: Microsoft";
        var schema = DocumentSchemas.For(DocumentType.DividendStatement);

        ExtractionResult result = await new LabelledLineExtractor()
            .ExtractAsync(new ExtractedText(text, false), schema);

        Assert.False(result.Fields.ContainsKey("ignore me"));
        Assert.Equal("Microsoft", result.Get("issuer"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — extractor types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/Entities/IEntityExtractor.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>
/// Pulls schema-defined fields out of recognized text. Only fields named in the schema are kept,
/// so arbitrary text in the document cannot inject unexpected keys (prompt-injection guard).
/// An LLM-backed extractor implements the same seam for messy formats.
/// </summary>
public interface IEntityExtractor
{
    Task<ExtractionResult> ExtractAsync(ExtractedText text, EntitySchema schema, CancellationToken ct = default);
}
```

Create `src/TaxClaw.Documents/Entities/LabelledLineExtractor.cs`:

```csharp
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>
/// Deterministic extractor for "label: value" text. Keeps only keys declared in the schema —
/// everything else is discarded, so document content is treated strictly as data.
/// </summary>
public sealed class LabelledLineExtractor : IEntityExtractor
{
    public Task<ExtractionResult> ExtractAsync(ExtractedText text, EntitySchema schema, CancellationToken ct = default)
    {
        var allowed = schema.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in text.Text.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            if (allowed.Contains(key) && value.Length > 0)
            {
                fields[key] = value;
            }
        }

        return Task.FromResult(new ExtractionResult(schema.Type, fields));
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS — only schema keys retained; `ignore me` dropped.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Documents/Entities/IEntityExtractor.cs src/TaxClaw.Documents/Entities/LabelledLineExtractor.cs tests/TaxClaw.Documents.Tests/LabelledLineExtractorTests.cs
git commit -m "feat(documents): add schema-bound entity extractor"
```

---

### Task 7: Map entities into the canonical return

**Files:**
- Create: `src/TaxClaw.Documents/Map/DocumentMapper.cs`
- Test: `tests/TaxClaw.Documents.Tests/DocumentMapperTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/DocumentMapperTests.cs`:

```csharp
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Map;
using TaxClaw.Documents.Model;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class DocumentMapperTests
{
    [Fact]
    public void Maps_a_dividend_extraction_to_an_income_item()
    {
        var extraction = new ExtractionResult(DocumentType.DividendStatement, new Dictionary<string, string>
        {
            ["issuer"] = "Microsoft",
            ["pay_date"] = "2027-03-10",
            ["gross_amount"] = "100.00",
            ["currency"] = "USD",
            ["withholding_tax"] = "15.00"
        });

        var ret = new TaxReturn(TaxYear.Of(2027));
        TaxReturn updated = new DocumentMapper().Apply(ret, extraction, documentId: "doc-1");

        IncomeItem item = Assert.Single(updated.Incomes);
        Assert.Equal("dividend", item.Kind);
        Assert.Equal(new Money(100.00m, "USD"), item.Amount);
        Assert.Equal(new DateOnly(2027, 3, 10), item.Date);
        Assert.Equal("doc-1", item.DocumentId);
    }

    [Fact]
    public void Unknown_type_is_a_no_op()
    {
        var extraction = new ExtractionResult(DocumentType.Unknown, new Dictionary<string, string>());
        var ret = new TaxReturn(TaxYear.Of(2027));

        TaxReturn updated = new DocumentMapper().Apply(ret, extraction, "doc-x");

        Assert.Empty(updated.Incomes);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — `DocumentMapper` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/Map/DocumentMapper.cs`:

```csharp
using System.Globalization;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;
using TaxClaw.Core.Model;

namespace TaxClaw.Documents.Map;

/// <summary>
/// Turns a validated extraction into canonical <see cref="IncomeItem"/>s on the return, attaching
/// the source document id for provenance. Parsing is culture-invariant and decimal-based.
/// </summary>
public sealed class DocumentMapper
{
    public TaxReturn Apply(TaxReturn ret, ExtractionResult extraction, string documentId) =>
        extraction.Type switch
        {
            DocumentType.DividendStatement => ret.WithIncome(new IncomeItem(
                "dividend",
                Money(extraction, "gross_amount", "currency"),
                Date(extraction, "pay_date"),
                documentId)),

            DocumentType.RsuVestingStatement => ret.WithIncome(new IncomeItem(
                "rsu_vesting",
                Money(extraction, "fmv_per_share", "currency").Multiply(Decimal(extraction, "shares")),
                Date(extraction, "vest_date"),
                documentId)),

            DocumentType.EmploymentIncomeStatement => ret.WithIncome(new IncomeItem(
                "employment",
                Money(extraction, "gross_income", "currency"),
                new DateOnly(ret.Year.Year, 12, 31),
                documentId)),

            _ => ret
        };

    private static Money Money(ExtractionResult e, string amountField, string currencyField) =>
        new(Decimal(e, amountField), e.Get(currencyField) ?? "CZK");

    private static decimal Decimal(ExtractionResult e, string field) =>
        decimal.Parse(e.Get(field) ?? "0", CultureInfo.InvariantCulture);

    private static DateOnly Date(ExtractionResult e, string field) =>
        DateOnly.ParseExact(e.Get(field) ?? "0001-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
```

Add a `Multiply` helper to `Money` so RSU mapping compiles — append to `src/TaxClaw.Core/Model/Money.cs` inside the `Money` struct:

```csharp
    public Money Multiply(decimal factor) =>
        this with { Amount = TaxClaw.Core.Math.DecimalMath.Multiply(Amount, factor) };
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS.

Run: `dotnet test tests/TaxClaw.Core.Tests`
Expected: PASS — `Money` change does not break existing tests.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Documents/Map src/TaxClaw.Core/Model/Money.cs tests/TaxClaw.Documents.Tests/DocumentMapperTests.cs
git commit -m "feat(documents): map extracted entities to canonical income items"
```

---

### Task 8: DocumentPipeline orchestration

**Files:**
- Create: `src/TaxClaw.Documents/DocumentPipeline.cs`
- Test: `tests/TaxClaw.Documents.Tests/DocumentPipelineTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Documents.Tests/DocumentPipelineTests.cs`:

```csharp
using System.Text;
using TaxClaw.Documents;
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Model;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Documents.Tests;

public class DocumentPipelineTests
{
    private sealed class PlainTextExtractor : ITextExtractor
    {
        public Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default) =>
            Task.FromResult<ExtractedText?>(new ExtractedText(Encoding.UTF8.GetString(doc.Bytes), false));
    }

    private sealed class ThrowingRecognizer : IRecognizer
    {
        public Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default) =>
            throw new InvalidOperationException("should not be called");
    }

    private static DocumentPipeline BuildPipeline() => new(
        new TextLayerDetector(new PlainTextExtractor(), new ThrowingRecognizer()),
        new KeywordClassifier(),
        new LabelledLineExtractor());

    [Fact]
    public async Task Processes_a_dividend_document_end_to_end()
    {
        const string text =
            "Dividend statement\nissuer: Microsoft\npay_date: 2027-03-10\n" +
            "gross_amount: 100.00\ncurrency: USD\nwithholding_tax: 15.00";
        var doc = SourceDocument.FromBytes("div.txt", Encoding.UTF8.GetBytes(text));

        var pipeline = BuildPipeline();
        DocumentResult result = await pipeline.ProcessAsync(doc, new TaxReturn(TaxYear.Of(2027)), "doc-1");

        Assert.Equal(DocumentType.DividendStatement, result.Type);
        Assert.True(result.Validation.IsValid);
        Assert.Single(result.Return.Incomes);
    }

    [Fact]
    public async Task Surfaces_validation_gaps_without_mapping()
    {
        const string text = "Dividend statement\nissuer: Microsoft"; // missing required fields
        var doc = SourceDocument.FromBytes("div.txt", Encoding.UTF8.GetBytes(text));

        DocumentResult result = await BuildPipeline()
            .ProcessAsync(doc, new TaxReturn(TaxYear.Of(2027)), "doc-2");

        Assert.False(result.Validation.IsValid);
        Assert.Contains("gross_amount", result.Validation.MissingFields);
        Assert.Empty(result.Return.Incomes); // not mapped while invalid
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: FAIL — `DocumentPipeline` / `DocumentResult` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Documents/DocumentPipeline.cs`:

```csharp
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Map;
using TaxClaw.Documents.Model;
using TaxClaw.Core.Model;

namespace TaxClaw.Documents;

/// <summary>The outcome of processing one document through the pipeline.</summary>
public sealed record DocumentResult(
    DocumentType Type,
    double Confidence,
    ExtractionResult Extraction,
    ValidationReport Validation,
    TaxReturn Return);

/// <summary>
/// Orchestrates: extract text → classify → extract entities (schema-bound) → validate →
/// map to the return only when valid. Invalid extractions are returned for the agent to
/// raise as questions, never silently mapped.
/// </summary>
public sealed class DocumentPipeline(
    TextLayerDetector extractor,
    IDocumentClassifier classifier,
    IEntityExtractor entityExtractor)
{
    private readonly DocumentMapper _mapper = new();

    public async Task<DocumentResult> ProcessAsync(
        SourceDocument doc, TaxReturn current, string documentId, CancellationToken ct = default)
    {
        ExtractedText text = await extractor.ExtractAsync(doc, ct);
        Classification classification = classifier.Classify(text);

        EntitySchema schema = DocumentSchemas.For(classification.Type);
        ExtractionResult extraction = await entityExtractor.ExtractAsync(text, schema, ct);
        ValidationReport validation = SchemaValidator.Validate(extraction, schema);

        TaxReturn updated = validation.IsValid
            ? _mapper.Apply(current, extraction, documentId)
            : current;

        return new DocumentResult(
            classification.Type, classification.Confidence, extraction, validation, updated);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Documents.Tests`
Expected: PASS — valid document maps; invalid one surfaces gaps and does not map.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 6: Commit**

```bash
git add src/TaxClaw.Documents/DocumentPipeline.cs tests/TaxClaw.Documents.Tests/DocumentPipelineTests.cs
git commit -m "feat(documents): add end-to-end document pipeline"
```

---

## Self-Review

**1. Spec coverage:**
- Drop a document; auto-detect type → Tasks 2, 4, 8. ✓
- Text-layer vs scan/photo (OCR/Vision path) → Task 3. ✓
- Schema-bound entity extraction per doc type (RSU/dividend/employment/trade) → Tasks 5, 6. ✓
- Map entities → canonical model with document provenance → Task 7. ✓
- Untrusted-data handling / prompt-injection guard (only schema keys kept) → Task 6. ✓
- Validation gaps surfaced for human confirmation, not silently mapped → Task 8. ✓
- *Deferred (noted):* AI-generated parser scripts for novel recurring formats reuse Plan 2's `ScriptCompiler` + `ApprovalGate` + sandbox; LLM-backed classifier/extractor implement the same seams (`IDocumentClassifier`, `IEntityExtractor`). These seams exist here; concrete LLM/OCR adapters and OS-level sandbox land alongside Plan 7's recognizer + privacy mode.

**2. Placeholder scan:** No TBD/TODO. Every code step is complete and compiles; tests assert real behavior. Deferrals reuse already-defined seams and are explicit. ✓

**3. Type consistency:** `ExtractedText(Text, UsedRecognition, PageCount)` consistent (2, 3, 4, 6). `ExtractionResult(Type, Fields)` + `.Get` consistent (5, 6, 7, 8). `EntitySchema(Type, Fields)` + `DocumentSchemas.For` consistent (5, 6, 8). `ValidationReport(IsValid, MissingFields)` consistent (5, 8). `IncomeItem(Kind, Money, DateOnly, DocumentId)` matches Plan 2's definition (7). `Money.Multiply(decimal)` added in Task 7 and used there. `DocumentPipeline.ProcessAsync(doc, current, documentId, ct)` consistent (8). ✓
