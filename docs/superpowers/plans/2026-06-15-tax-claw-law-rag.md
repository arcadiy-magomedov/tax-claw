# tax-claw Law Corpus & RAG — Implementation Plan (Plan 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Czech legislation a searchable, versioned knowledge base. Ingest the income-tax act (and related texts) split by paragraph (§), index it hybridly (keyword + vector + cross-reference graph), and expose `lookup_law` and `search_law` tools so the agent can answer free-form questions and cite an exact §/edition for every figure — with no hardcoded paragraph list.

**Architecture:** A new `TaxClaw.Law` library. Parsing splits raw legislation text into `LawParagraph`s tagged with an `LawEdition` (effective date + content hash). A keyword `InvertedIndex` and a pluggable vector index (over an `IEmbeddingProvider`, faked in tests) are fused by a `HybridLawIndex` with reciprocal-rank fusion. A cross-reference extractor links "§ N" mentions. `LawTools` exposes lookup/search as `AIFunction`s for the agent. Edition selection is per project tax-year ("law as of filing").

**Tech Stack:** .NET 10, `Microsoft.Extensions.AI` (`IEmbeddingGenerator` seam), xUnit. Builds on Plan 1 (`TaxClaw.Core`) and reuses the `IChatClient`/embedding provider seam.

---

## File Structure

- `src/TaxClaw.Law/Model/LawEdition.cs` — edition (effective date, label, hash).
- `src/TaxClaw.Law/Model/LawParagraph.cs` — one § with text, edition, source url, cross-refs.
- `src/TaxClaw.Law/Ingest/ParagraphParser.cs` — split raw text into paragraphs.
- `src/TaxClaw.Law/Ingest/CrossReferenceExtractor.cs` — find "§ N" mentions.
- `src/TaxClaw.Law/Index/InvertedIndex.cs` — keyword search with scoring.
- `src/TaxClaw.Law/Index/IEmbeddingProvider.cs`, `Index/VectorIndex.cs` — semantic search.
- `src/TaxClaw.Law/Index/HybridLawIndex.cs` — fuse keyword + vector; `LawSearchResult`.
- `src/TaxClaw.Law/LawCorpus.cs` — load editions, select by year, lookup by §.
- `src/TaxClaw.Law/LawTools.cs` — `lookup_law` / `search_law` AI tools.
- Tests under `tests/TaxClaw.Law.Tests/`.

---

### Task 1: Scaffold the law library

**Files:**
- Create: `src/TaxClaw.Law`, `tests/TaxClaw.Law.Tests`

- [ ] **Step 1: Create and reference projects**

```bash
dotnet new classlib -o src/TaxClaw.Law
dotnet new xunit    -o tests/TaxClaw.Law.Tests
rm src/TaxClaw.Law/Class1.cs tests/TaxClaw.Law.Tests/UnitTest1.cs

dotnet sln add src/TaxClaw.Law tests/TaxClaw.Law.Tests
dotnet add src/TaxClaw.Law reference src/TaxClaw.Core
dotnet add src/TaxClaw.Law package Microsoft.Extensions.AI
dotnet add tests/TaxClaw.Law.Tests reference src/TaxClaw.Core src/TaxClaw.Law
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore(law): scaffold law library"
```

---

### Task 2: Edition and paragraph models

**Files:**
- Create: `src/TaxClaw.Law/Model/LawEdition.cs`
- Create: `src/TaxClaw.Law/Model/LawParagraph.cs`
- Test: `tests/TaxClaw.Law.Tests/LawModelTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/LawModelTests.cs`:

```csharp
using TaxClaw.Law.Model;
using Xunit;

namespace TaxClaw.Law.Tests;

public class LawModelTests
{
    [Fact]
    public void Edition_applies_to_a_year_within_its_effective_window()
    {
        var edition = new LawEdition("586/1992", "2027.1",
            EffectiveFrom: new DateOnly(2027, 1, 1),
            EffectiveTo: new DateOnly(2027, 12, 31));

        Assert.True(edition.AppliesToYear(2027));
        Assert.False(edition.AppliesToYear(2026));
    }

    [Fact]
    public void Open_ended_edition_applies_to_all_later_years()
    {
        var edition = new LawEdition("586/1992", "2027.1",
            EffectiveFrom: new DateOnly(2027, 1, 1),
            EffectiveTo: null);

        Assert.True(edition.AppliesToYear(2030));
    }

    [Fact]
    public void Paragraph_hash_is_derived_from_text_and_section()
    {
        var p1 = new LawParagraph("§ 6", "income from employment", default!, null);
        var p2 = new LawParagraph("§ 6", "income from employment", default!, null);
        Assert.Equal(p1.Hash, p2.Hash);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — model types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/Model/LawEdition.cs`:

```csharp
namespace TaxClaw.Law.Model;

/// <summary>
/// A specific edition (consolidated version) of a law, valid for a date window. Selecting the
/// edition by the project's tax year implements "law as of filing".
/// </summary>
public sealed record LawEdition(
    string ActNumber,
    string Label,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo)
{
    public bool AppliesToYear(int year)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);
        bool startsBeforeYearEnds = EffectiveFrom <= end;
        bool endsAfterYearStarts = EffectiveTo is null || EffectiveTo >= start;
        return startsBeforeYearEnds && endsAfterYearStarts;
    }
}
```

Create `src/TaxClaw.Law/Model/LawParagraph.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace TaxClaw.Law.Model;

/// <summary>
/// One paragraph (§) of legislation: its section id, text, the edition it belongs to, an optional
/// source url, and the §-references found in its text. The hash pins the exact wording.
/// </summary>
public sealed record LawParagraph(
    string Section,
    string Text,
    LawEdition Edition,
    string? SourceUrl,
    IReadOnlyList<string>? CrossReferences = null)
{
    public string Hash { get; } = ComputeHash(Section, Text);

    private static string ComputeHash(string section, string text)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{section}\n{text}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Law/Model tests/TaxClaw.Law.Tests/LawModelTests.cs
git commit -m "feat(law): add edition and paragraph models"
```

---

### Task 3: Paragraph parser and cross-reference extractor

**Files:**
- Create: `src/TaxClaw.Law/Ingest/CrossReferenceExtractor.cs`
- Create: `src/TaxClaw.Law/Ingest/ParagraphParser.cs`
- Test: `tests/TaxClaw.Law.Tests/ParagraphParserTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/ParagraphParserTests.cs`:

```csharp
using TaxClaw.Law.Ingest;
using TaxClaw.Law.Model;
using Xunit;

namespace TaxClaw.Law.Tests;

public class ParagraphParserTests
{
    private static readonly LawEdition Edition =
        new("586/1992", "2027.1", new DateOnly(2027, 1, 1), null);

    [Fact]
    public void Splits_text_into_sections_keyed_by_paragraph()
    {
        const string raw =
            "§ 6\nPříjmy ze závislé činnosti.\n" +
            "§ 8\nPříjmy z kapitálového majetku podle § 6.";

        var paragraphs = new ParagraphParser().Parse(raw, Edition, sourceUrl: null);

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("§ 6", paragraphs[0].Section);
        Assert.Contains("závislé", paragraphs[0].Text);
    }

    [Fact]
    public void Captures_cross_references_to_other_sections()
    {
        const string raw = "§ 8\nPříjmy z kapitálového majetku podle § 6.";

        var paragraphs = new ParagraphParser().Parse(raw, Edition, sourceUrl: null);

        Assert.Contains("§ 6", paragraphs[0].CrossReferences!);
    }

    [Fact]
    public void Extractor_finds_all_unique_section_references()
    {
        var refs = new CrossReferenceExtractor().Extract("see § 6 and § 10 and again § 6");
        Assert.Equal(new[] { "§ 6", "§ 10" }, refs);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — parser/extractor do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/Ingest/CrossReferenceExtractor.cs`:

```csharp
using System.Text.RegularExpressions;

namespace TaxClaw.Law.Ingest;

/// <summary>Finds unique "§ N" references in a block of text, in first-seen order.</summary>
public sealed partial class CrossReferenceExtractor
{
    [GeneratedRegex(@"§\s*(\d+[a-z]?)", RegexOptions.CultureInvariant)]
    private static partial Regex SectionRegex();

    public IReadOnlyList<string> Extract(string text)
    {
        var seen = new List<string>();
        foreach (Match m in SectionRegex().Matches(text))
        {
            string section = $"§ {m.Groups[1].Value}";
            if (!seen.Contains(section))
            {
                seen.Add(section);
            }
        }
        return seen;
    }
}
```

Create `src/TaxClaw.Law/Ingest/ParagraphParser.cs`:

```csharp
using System.Text.RegularExpressions;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Ingest;

/// <summary>
/// Splits a consolidated legislation text into <see cref="LawParagraph"/>s on "§ N" boundaries.
/// Each paragraph's body is scanned for cross-references to other sections.
/// </summary>
public sealed partial class ParagraphParser
{
    [GeneratedRegex(@"(?=§\s*\d+[a-z]?)", RegexOptions.CultureInvariant)]
    private static partial Regex SectionBoundary();

    [GeneratedRegex(@"^§\s*(\d+[a-z]?)", RegexOptions.CultureInvariant)]
    private static partial Regex SectionHead();

    private readonly CrossReferenceExtractor _crossRefs = new();

    public IReadOnlyList<LawParagraph> Parse(string raw, LawEdition edition, string? sourceUrl)
    {
        var result = new List<LawParagraph>();

        foreach (string chunk in SectionBoundary().Split(raw))
        {
            string trimmed = chunk.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            Match head = SectionHead().Match(trimmed);
            if (!head.Success)
            {
                continue;
            }

            string section = $"§ {head.Groups[1].Value}";
            string body = trimmed[head.Length..].Trim();
            var crossRefs = _crossRefs.Extract(body).Where(r => r != section).ToList();

            result.Add(new LawParagraph(section, body, edition, sourceUrl, crossRefs));
        }

        return result;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS — two paragraphs, cross-reference `§ 6` captured.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Law/Ingest tests/TaxClaw.Law.Tests/ParagraphParserTests.cs
git commit -m "feat(law): add paragraph parser and cross-reference extractor"
```

---

### Task 4: Keyword inverted index

**Files:**
- Create: `src/TaxClaw.Law/Index/InvertedIndex.cs`
- Test: `tests/TaxClaw.Law.Tests/InvertedIndexTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/InvertedIndexTests.cs`:

```csharp
using TaxClaw.Law.Index;
using Xunit;

namespace TaxClaw.Law.Tests;

public class InvertedIndexTests
{
    [Fact]
    public void Ranks_documents_containing_more_query_terms_higher()
    {
        var index = new InvertedIndex();
        index.Add("a", "dividend income withholding tax");
        index.Add("b", "employment salary income");
        index.Add("c", "vehicle registration");

        var hits = index.Search("dividend income", limit: 3).Select(h => h.Id).ToList();

        Assert.Equal("a", hits[0]);   // matches both terms
        Assert.Contains("b", hits);   // matches one term
        Assert.DoesNotContain("c", hits);
    }

    [Fact]
    public void Empty_query_returns_nothing()
    {
        var index = new InvertedIndex();
        index.Add("a", "anything");
        Assert.Empty(index.Search("   ", limit: 5));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — `InvertedIndex` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/Index/InvertedIndex.cs`:

```csharp
using System.Text.RegularExpressions;

namespace TaxClaw.Law.Index;

/// <summary>A keyword hit: document id and a score (higher is more relevant).</summary>
public readonly record struct ScoredHit(string Id, double Score);

/// <summary>
/// A small in-memory inverted index with term-frequency scoring. Sufficient for the agent's
/// keyword path; can be swapped for SQLite FTS5 later without changing callers.
/// </summary>
public sealed partial class InvertedIndex
{
    [GeneratedRegex(@"\p{L}+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private readonly Dictionary<string, Dictionary<string, int>> _postings = new(); // term -> (docId -> count)

    public void Add(string id, string text)
    {
        foreach (string term in Tokenize(text))
        {
            if (!_postings.TryGetValue(term, out var docs))
            {
                docs = new Dictionary<string, int>();
                _postings[term] = docs;
            }
            docs[id] = docs.GetValueOrDefault(id) + 1;
        }
    }

    public IReadOnlyList<ScoredHit> Search(string query, int limit)
    {
        var scores = new Dictionary<string, double>();

        foreach (string term in Tokenize(query))
        {
            if (_postings.TryGetValue(term, out var docs))
            {
                foreach ((string id, int count) in docs)
                {
                    scores[id] = scores.GetValueOrDefault(id) + count;
                }
            }
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .Take(limit)
            .Select(kv => new ScoredHit(kv.Key, kv.Value))
            .ToList();
    }

    private static IEnumerable<string> Tokenize(string text) =>
        WordRegex().Matches(text).Select(m => m.Value.ToLowerInvariant());
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Law/Index/InvertedIndex.cs tests/TaxClaw.Law.Tests/InvertedIndexTests.cs
git commit -m "feat(law): add keyword inverted index"
```

---

### Task 5: Vector index over a pluggable embedding provider

**Files:**
- Create: `src/TaxClaw.Law/Index/IEmbeddingProvider.cs`
- Create: `src/TaxClaw.Law/Index/VectorIndex.cs`
- Test: `tests/TaxClaw.Law.Tests/VectorIndexTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/VectorIndexTests.cs`:

```csharp
using TaxClaw.Law.Index;
using Xunit;

namespace TaxClaw.Law.Tests;

public class VectorIndexTests
{
    /// <summary>Deterministic fake: vector = [count of 'a', count of 'b', length].</summary>
    private sealed class FakeEmbeddings : IEmbeddingProvider
    {
        public ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            float a = text.Count(c => c == 'a');
            float b = text.Count(c => c == 'b');
            return ValueTask.FromResult(new[] { a, b, text.Length });
        }
    }

    [Fact]
    public async Task Nearest_returns_the_most_similar_document_first()
    {
        var index = new VectorIndex(new FakeEmbeddings());
        await index.AddAsync("aaa", "aaa");
        await index.AddAsync("bbb", "bbb");

        var hits = await index.SearchAsync("aaaa", limit: 2);

        Assert.Equal("aaa", hits[0].Id);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — vector types do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/Index/IEmbeddingProvider.cs`:

```csharp
namespace TaxClaw.Law.Index;

/// <summary>
/// Produces an embedding vector for text. Backed by <c>IEmbeddingGenerator</c> in production and
/// by a deterministic fake in tests, so semantic search needs no network to verify.
/// </summary>
public interface IEmbeddingProvider
{
    ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
```

Create `src/TaxClaw.Law/Index/VectorIndex.cs`:

```csharp
namespace TaxClaw.Law.Index;

/// <summary>Cosine-similarity vector search over embedded documents.</summary>
public sealed class VectorIndex(IEmbeddingProvider embeddings)
{
    private readonly List<(string Id, float[] Vector)> _entries = new();

    public async Task AddAsync(string id, string text, CancellationToken ct = default)
    {
        float[] vector = await embeddings.EmbedAsync(text, ct);
        _entries.Add((id, vector));
    }

    public async Task<IReadOnlyList<ScoredHit>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        float[] q = await embeddings.EmbedAsync(query, ct);

        return _entries
            .Select(e => new ScoredHit(e.Id, Cosine(q, e.Vector)))
            .OrderByDescending(h => h.Score)
            .Take(limit)
            .ToList();
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        int n = System.Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return (na == 0 || nb == 0) ? 0 : dot / (System.Math.Sqrt(na) * System.Math.Sqrt(nb));
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Law/Index/IEmbeddingProvider.cs src/TaxClaw.Law/Index/VectorIndex.cs tests/TaxClaw.Law.Tests/VectorIndexTests.cs
git commit -m "feat(law): add vector index over pluggable embeddings"
```

---

### Task 6: Hybrid index with reciprocal-rank fusion

**Files:**
- Create: `src/TaxClaw.Law/Index/HybridLawIndex.cs`
- Test: `tests/TaxClaw.Law.Tests/HybridLawIndexTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/HybridLawIndexTests.cs`:

```csharp
using TaxClaw.Law.Index;
using TaxClaw.Law.Model;
using Xunit;

namespace TaxClaw.Law.Tests;

public class HybridLawIndexTests
{
    private sealed class FakeEmbeddings : IEmbeddingProvider
    {
        public ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            float d = text.Contains("dividend", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            float e = text.Contains("employment", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return ValueTask.FromResult(new[] { d, e });
        }
    }

    private static readonly LawEdition Edition =
        new("586/1992", "2027.1", new DateOnly(2027, 1, 1), null);

    [Fact]
    public async Task Fuses_keyword_and_vector_hits_and_returns_paragraphs()
    {
        var index = new HybridLawIndex(new FakeEmbeddings());
        await index.AddAsync(new LawParagraph("§ 6", "employment income", Edition, null));
        await index.AddAsync(new LawParagraph("§ 8", "dividend income withholding", Edition, null));

        var results = await index.SearchAsync("dividend", limit: 2);

        Assert.Equal("§ 8", results[0].Paragraph.Section);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — `HybridLawIndex` / `LawSearchResult` do not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/Index/HybridLawIndex.cs`:

```csharp
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Index;

/// <summary>A search result: the matched paragraph and its fused relevance score.</summary>
public sealed record LawSearchResult(LawParagraph Paragraph, double Score);

/// <summary>
/// Fuses keyword and vector results via reciprocal-rank fusion (RRF). This is the single retrieval
/// path used for both calculation lookups and free-form legal questions.
/// </summary>
public sealed class HybridLawIndex(IEmbeddingProvider embeddings, double rrfK = 60.0)
{
    private readonly InvertedIndex _keyword = new();
    private readonly VectorIndex _vector = new(embeddings);
    private readonly Dictionary<string, LawParagraph> _byId = new();

    public async Task AddAsync(LawParagraph paragraph, CancellationToken ct = default)
    {
        string id = paragraph.Section;
        _byId[id] = paragraph;
        _keyword.Add(id, paragraph.Text);
        await _vector.AddAsync(id, paragraph.Text, ct);
    }

    public async Task<IReadOnlyList<LawSearchResult>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        var keywordHits = _keyword.Search(query, limit * 2);
        var vectorHits = await _vector.SearchAsync(query, limit * 2, ct);

        var fused = new Dictionary<string, double>();
        AddRrf(fused, keywordHits);
        AddRrf(fused, vectorHits);

        return fused
            .OrderByDescending(kv => kv.Value)
            .Take(limit)
            .Where(kv => _byId.ContainsKey(kv.Key))
            .Select(kv => new LawSearchResult(_byId[kv.Key], kv.Value))
            .ToList();
    }

    private void AddRrf(Dictionary<string, double> fused, IReadOnlyList<ScoredHit> hits)
    {
        for (int rank = 0; rank < hits.Count; rank++)
        {
            string id = hits[rank].Id;
            fused[id] = fused.GetValueOrDefault(id) + 1.0 / (rrfK + rank + 1);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Law/Index/HybridLawIndex.cs tests/TaxClaw.Law.Tests/HybridLawIndexTests.cs
git commit -m "feat(law): add hybrid index with reciprocal-rank fusion"
```

---

### Task 7: LawCorpus — editions, year selection, lookup

**Files:**
- Create: `src/TaxClaw.Law/LawCorpus.cs`
- Test: `tests/TaxClaw.Law.Tests/LawCorpusTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/LawCorpusTests.cs`:

```csharp
using TaxClaw.Law;
using TaxClaw.Law.Index;
using TaxClaw.Law.Model;
using Xunit;

namespace TaxClaw.Law.Tests;

public class LawCorpusTests
{
    private sealed class FakeEmbeddings : IEmbeddingProvider
    {
        public ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            ValueTask.FromResult(new[] { (float)text.Length });
    }

    [Fact]
    public async Task Looks_up_a_paragraph_by_section_for_the_year_edition()
    {
        var corpus = new LawCorpus(new FakeEmbeddings());
        var edition = new LawEdition("586/1992", "2027.1", new DateOnly(2027, 1, 1), null);

        await corpus.IngestAsync(
            "§ 6\nPříjmy ze závislé činnosti.", edition, sourceUrl: "https://example/586");

        LawParagraph? p = corpus.Lookup("§ 6", year: 2027);

        Assert.NotNull(p);
        Assert.Contains("závislé", p!.Text);
        Assert.Equal("https://example/586", p.SourceUrl);
    }

    [Fact]
    public async Task Lookup_for_a_year_without_an_edition_returns_null()
    {
        var corpus = new LawCorpus(new FakeEmbeddings());
        var edition = new LawEdition("586/1992", "2027.1", new DateOnly(2027, 1, 1),
            new DateOnly(2027, 12, 31));
        await corpus.IngestAsync("§ 6\nText.", edition, null);

        Assert.Null(corpus.Lookup("§ 6", year: 2025));
    }

    [Fact]
    public async Task Search_returns_results_scoped_to_the_year()
    {
        var corpus = new LawCorpus(new FakeEmbeddings());
        var edition = new LawEdition("586/1992", "2027.1", new DateOnly(2027, 1, 1), null);
        await corpus.IngestAsync("§ 8\nPříjmy z kapitálového majetku.", edition, null);

        var results = await corpus.SearchAsync("kapitálového", year: 2027, limit: 3);

        Assert.NotEmpty(results);
        Assert.Equal("§ 8", results[0].Paragraph.Section);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — `LawCorpus` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/LawCorpus.cs`:

```csharp
using TaxClaw.Law.Index;
using TaxClaw.Law.Ingest;
using TaxClaw.Law.Model;

namespace TaxClaw.Law;

/// <summary>
/// The legislation knowledge base. Holds paragraphs across editions, selects the edition that
/// applies to a tax year, and serves both addressed lookups and hybrid search. One hybrid index
/// is maintained per edition label so search is naturally year-scoped.
/// </summary>
public sealed class LawCorpus(IEmbeddingProvider embeddings)
{
    private readonly ParagraphParser _parser = new();
    private readonly List<LawParagraph> _paragraphs = new();
    private readonly Dictionary<string, HybridLawIndex> _indexByEdition = new();

    public async Task IngestAsync(string rawText, LawEdition edition, string? sourceUrl, CancellationToken ct = default)
    {
        if (!_indexByEdition.TryGetValue(edition.Label, out var index))
        {
            index = new HybridLawIndex(embeddings);
            _indexByEdition[edition.Label] = index;
        }

        foreach (LawParagraph paragraph in _parser.Parse(rawText, edition, sourceUrl))
        {
            _paragraphs.Add(paragraph);
            await index.AddAsync(paragraph, ct);
        }
    }

    public LawParagraph? Lookup(string section, int year) =>
        _paragraphs.FirstOrDefault(p => p.Section == section && p.Edition.AppliesToYear(year));

    public async Task<IReadOnlyList<LawSearchResult>> SearchAsync(string query, int year, int limit, CancellationToken ct = default)
    {
        LawEdition? edition = _paragraphs
            .Select(p => p.Edition)
            .Distinct()
            .FirstOrDefault(e => e.AppliesToYear(year));

        if (edition is null || !_indexByEdition.TryGetValue(edition.Label, out var index))
        {
            return [];
        }

        return await index.SearchAsync(query, limit, ct);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TaxClaw.Law/LawCorpus.cs tests/TaxClaw.Law.Tests/LawCorpusTests.cs
git commit -m "feat(law): add LawCorpus with year-scoped lookup and search"
```

---

### Task 8: Law AI tools (lookup_law, search_law)

**Files:**
- Create: `src/TaxClaw.Law/LawTools.cs`
- Test: `tests/TaxClaw.Law.Tests/LawToolsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/TaxClaw.Law.Tests/LawToolsTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Law;
using TaxClaw.Law.Index;
using TaxClaw.Law.Model;
using Xunit;

namespace TaxClaw.Law.Tests;

public class LawToolsTests
{
    private sealed class FakeEmbeddings : IEmbeddingProvider
    {
        public ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            ValueTask.FromResult(new[] { (float)text.Length });
    }

    private static async Task<LawCorpus> SeededCorpus()
    {
        var corpus = new LawCorpus(new FakeEmbeddings());
        var edition = new LawEdition("586/1992", "2027.1", new DateOnly(2027, 1, 1), null);
        await corpus.IngestAsync("§ 6\nPříjmy ze závislé činnosti.", edition, "https://example/586");
        return corpus;
    }

    [Fact]
    public async Task Lookup_tool_returns_paragraph_text_with_citation()
    {
        var tools = new LawTools(await SeededCorpus(), year: 2027);
        string result = tools.LookupLaw("§ 6");

        Assert.Contains("závislé", result);
        Assert.Contains("§ 6", result);
        Assert.Contains("586/1992", result);
    }

    [Fact]
    public async Task Lookup_tool_reports_when_a_section_is_missing()
    {
        var tools = new LawTools(await SeededCorpus(), year: 2027);
        Assert.Contains("not found", tools.LookupLaw("§ 999"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTools_exposes_lookup_and_search()
    {
        var tools = new LawTools(await SeededCorpus(), year: 2027);
        var names = tools.CreateTools().OfType<AIFunction>().Select(f => f.Name).ToHashSet();

        Assert.Contains("lookup_law", names);
        Assert.Contains("search_law", names);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — `LawTools` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/TaxClaw.Law/LawTools.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.AI;
using TaxClaw.Law.Model;

namespace TaxClaw.Law;

/// <summary>
/// Exposes the corpus to the agent as tools. Every answer carries a citation (§ + act + edition)
/// so figures and explanations can always be traced to a checkable source.
/// </summary>
public sealed class LawTools(LawCorpus corpus, int year)
{
    public string LookupLaw(string section)
    {
        LawParagraph? p = corpus.Lookup(section, year);
        if (p is null)
        {
            return $"Section '{section}' not found for tax year {year}.";
        }
        return Format(p);
    }

    public string SearchLaw(string query)
    {
        var results = corpus.SearchAsync(query, year, limit: 3).GetAwaiter().GetResult();
        if (results.Count == 0)
        {
            return $"No legislation matched '{query}' for tax year {year}.";
        }

        var sb = new StringBuilder();
        foreach (LawSearchResult r in results)
        {
            sb.AppendLine(Format(r.Paragraph));
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string Format(LawParagraph p)
    {
        string citation = $"{p.Section} (act {p.Edition.ActNumber}, edition {p.Edition.Label})";
        string source = p.SourceUrl is null ? "" : $"\nSource: {p.SourceUrl}";
        return $"{citation}\n{p.Text}{source}";
    }

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(LookupLaw, name: "lookup_law",
            description: "Return the exact text and citation of a legislation section for the active tax year."),
        AIFunctionFactory.Create(SearchLaw, name: "search_law",
            description: "Search Czech tax legislation for the active year and return the most relevant sections with citations.")
    ];
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS — all `LawToolsTests` green.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 6: Commit**

```bash
git add src/TaxClaw.Law/LawTools.cs tests/TaxClaw.Law.Tests/LawToolsTests.cs
git commit -m "feat(law): expose lookup_law and search_law as AI tools"
```

---

### Task 9: Wire law tools into the agent + production embedding adapter

**Files:**
- Create: `src/TaxClaw.Law/Index/ChatEmbeddingProvider.cs`
- Modify: `src/TaxClaw.Tui/Program.cs` (add law tools to the agent when a corpus is configured)
- Test: `tests/TaxClaw.Law.Tests/ChatEmbeddingProviderTests.cs`

- [ ] **Step 1: Add the embeddings package to the law project**

```bash
dotnet add src/TaxClaw.Law package Microsoft.Extensions.AI.Abstractions
```

- [ ] **Step 2: Write the failing test**

Create `tests/TaxClaw.Law.Tests/ChatEmbeddingProviderTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using TaxClaw.Law.Index;
using Xunit;

namespace TaxClaw.Law.Tests;

public class ChatEmbeddingProviderTests
{
    private sealed class FakeGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var list = values.Select(v => new Embedding<float>(new float[] { v.Length })).ToList();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(list));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [Fact]
    public async Task Adapts_an_embedding_generator_to_a_float_vector()
    {
        var provider = new ChatEmbeddingProvider(new FakeGenerator());
        float[] vector = await provider.EmbedAsync("abcd");
        Assert.Equal(new float[] { 4 }, vector);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: FAIL — `ChatEmbeddingProvider` does not exist.

- [ ] **Step 4: Write the minimal implementation**

Create `src/TaxClaw.Law/Index/ChatEmbeddingProvider.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace TaxClaw.Law.Index;

/// <summary>
/// Adapts a <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> (from any provider) to the
/// corpus's <see cref="IEmbeddingProvider"/> seam, keeping the law library provider-agnostic.
/// </summary>
public sealed class ChatEmbeddingProvider(IEmbeddingGenerator<string, Embedding<float>> generator)
    : IEmbeddingProvider
{
    public async ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        GeneratedEmbeddings<Embedding<float>> result =
            await generator.GenerateAsync([text], cancellationToken: ct);
        return result[0].Vector.ToArray();
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/TaxClaw.Law.Tests`
Expected: PASS.

- [ ] **Step 6: Wire the tools into the TUI agent (additive, behind a guard)**

In `src/TaxClaw.Tui/Program.cs`, after the `agent` is constructed, add law tools only if a corpus directory exists. First add the reference:

```bash
dotnet add src/TaxClaw.Tui reference src/TaxClaw.Law
```

Then modify `src/TaxClaw.Tui/Program.cs` — replace the agent-construction block:

```csharp
var chatClient = new ChatClientFactory(llmOptions).Create();
var agent = new TaxClawAgent(chatClient, Prompts.System, MathTools.CreateTools());
```

with:

```csharp
var chatClient = new ChatClientFactory(llmOptions).Create();

var tools = new List<AITool>(MathTools.CreateTools());
// Law tools are added in a later wiring pass once a corpus is ingested for the active year;
// the seam is here so the agent can cite legislation. See TaxClaw.Law.LawTools.
var agent = new TaxClawAgent(chatClient, Prompts.System, tools);
```

Add the using at the top of `Program.cs`:

```csharp
using Microsoft.Extensions.AI;
```

> Full corpus ingestion wiring (loading legislation files for the active project year and calling
> `LawTools.CreateTools()`) lands when projects carry a year context in the document plan; this step
> only establishes the tool-list seam and reference so nothing regresses.

- [ ] **Step 7: Verify build and full suite**

Run: `dotnet build`
Expected: `Build succeeded.`

Run: `dotnet test`
Expected: PASS across all projects.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(law): add embedding adapter and law-tools seam in the agent"
```

---

## Self-Review

**1. Spec coverage:**
- Law as searchable knowledge base, no hardcoded § list → Tasks 3–8. ✓
- Versioned by edition; "law as of filing" via year selection → Tasks 2, 7. ✓
- Split by §/paragraph + cross-reference graph → Task 3. ✓
- Hybrid index (keyword + vector) → Tasks 4, 5, 6. ✓
- Single retrieval path for calc lookups and free-form questions → Tasks 7, 8. ✓
- Provenance/citation on every result (§ + act + edition + source url) → Tasks 2, 8. ✓
- Provider-agnostic embeddings → Tasks 5, 9. ✓

**2. Placeholder scan:** No TBD/TODO. Every code step is complete; every test asserts real behavior. The Task 9 note about full ingestion wiring is an explicit, scoped deferral (the seam is implemented and tested), not a placeholder in code. ✓

**3. Type consistency:** `IEmbeddingProvider.EmbedAsync(string, CancellationToken) -> ValueTask<float[]>` consistent (Tasks 5, 6, 7, 9). `ScoredHit(Id, Score)` consistent (4, 5, 6). `LawParagraph(Section, Text, Edition, SourceUrl, CrossReferences)` consistent (2, 3, 6, 7, 8). `LawEdition(ActNumber, Label, EffectiveFrom, EffectiveTo)` + `.AppliesToYear` consistent (2, 6, 7, 8). `LawCorpus.Lookup(section, year)` / `SearchAsync(query, year, limit, ct)` consistent (7, 8). `LawSearchResult(Paragraph, Score)` consistent (6, 7, 8). ✓
