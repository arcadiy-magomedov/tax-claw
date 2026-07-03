# tax-claw Law Grounding & Retrieval — Implementation Plan (Plan 3, revised)

> **Revised 2026-07-03.** This supersedes the original "Law Corpus & RAG" plan. The rewrite is
> driven by (a) the requirement that **every agent decision must be grounded in the in-force
> legislation**, and (b) an investigation that resolved the legislation source and measured
> retrieval. See `tools/law-retrieval-eval/` for the measurement. Key changes vs the original:
> the fragile paragraph parser/regex is **removed** (the official source is already structured);
> hybrid vector+RRF is **deferred** (measured unnecessary for v1); the plan now centers on a
> **grounding contract** enforced by gates, not on a retrieval engine.

**Goal:** Make the in-force Czech Income Tax Act a first-class, versioned, **grounding** substrate:
every figure and every legal claim the agent surfaces resolves to an exact § in the edition that
applies to the project's tax year — verifiably, or it is flagged for human confirmation.

**Non-goal (v1):** a bespoke hybrid RAG engine. Retrieval is a thin, pluggable detail; the value is
the grounding contract + an authoritative versioned corpus.

---

## 1. What the investigation established (evidence, not assumptions)

**Source — RESOLVED (closes spec §14).** The official **e-Sbírka open data**
(`opendata.eselpoint.gov.cz`: SPARQL + LodView + bulk JSON + REST) is keyless, **public-domain**
(§3 Czech Copyright Act: official works are not copyrighted), and updated daily. `zakonyprolidi.cz`
is off-limits (ToS forbids bulk extraction).

**No parser needed.** The source is natively structured: `část → § (par_6a, par_38ma) → odstavec →
písmeno → bod`, letter-suffixed sections handled by the source, and each fragment carries an
official **citation label** (`"§ 33 odst. 3"`) plus its text (`text-fragmentu`). Editions are
**date-addressed** ELI URIs (`eli/cz/sb/1992/586/2027-01-01`). ⇒ Ingestion is a **structured
import**, not text parsing. The original `ParagraphParser`/regex (which mis-split on inline "§"
references and mishandled multi-letter suffixes) is dropped entirely.

**Retrieval — MEASURED** (`tools/law-retrieval-eval/`, 199 §, 22 gold queries, 2027 edition):

| strategy | R@1 | R@3 | R@5 | MRR |
|---|---|---|---|---|
| raw English keyword | 0.00 | 0.00 | 0.00 | 0.00 |
| keyword + Czech query-expansion (FTS5) | 0.50 | 0.77 | 0.77 | 0.61 |

Conclusions: (1) queries **must** be in Czech legal vocabulary — raw EN = 0 recall; (2) keyword +
expansion is strong on *discovery* queries (RSU, dividends, sale of securities, time test, foreign
credit, deductions → rank 1–3) but misses high-frequency "definitional" §s (rate §16, taxpayer §2,
base §5, credit §35ba, return §38g); (3) those misses are exactly what **addressed `lookup_law(§)`
covers** (the agent knows them from the form/instructions). ⇒ **v1 retrieval = addressed lookup +
FTS5 keyword with mandatory Czech expansion. Vector/RRF deferred** behind a seam.

---

## 2. The grounding contract (the core of this plan)

"Every agent decision grounded in the in-force law" is made an **invariant enforced by gates**, not
a prompt hope. Four elements:

1. **Authoritative versioned corpus (`ILawCorpus`)** — resolves `(section, LawVersion)` to a
   `LawSection` with official citation, source ELI, and content hash. Single source of truth for
   *what the law says and when*.
2. **Per-project version pinning (`LawVersionSet`)** — the project pins `{act → edition}` by an
   explicit policy for its tax year. Deterministic (no `FirstOrDefault`-over-overlaps). Every gate
   resolves against it. Feeds Plan 5's version-invalidation of learned artifacts.
3. **Numbers gate (approval-time, reuses Plan 2)** — a generated calc function cannot be approved
   /registered unless its `Provenance.LawRef` resolves to §(s) present in the pinned edition.
   ⇒ ungrounded arithmetic is impossible *by construction*.
4. **Claims gate (runtime, MAF middleware)** — the agent's legal assertions must cite §; middleware
   verifies each citation resolves in the pinned edition (and quoted text matches the corpus). Un-
   grounded legal claims are flagged for human confirmation; verified ones get citations appended.
   ⇒ *all* decisions (not only numbers) are grounded.

Retrieval (`lookup_law`/`search_law`) is how the agent *finds* the law to cite; the gates are how
grounding is *enforced*.

---

## 3. Architecture & file structure

New library `TaxClaw.Law` (depends on `TaxClaw.Core`; `Microsoft.Extensions.AI` for tools).

- `Model/LawVersion.cs` — `(ActNumber, EffectiveOn)`; ELI-addressable edition.
- `Model/LawSection.cs` — aggregated §: `(Section, CitationLabel, Text, Version, SourceEli, Hash)`.
- `Model/LawVersionSet.cs` — per-project `{act → edition}`; `EditionFor(taxYear)`.
- `Ingest/ILawSource.cs`, `Ingest/ESbirkaSource.cs` — pull an edition's fragments and **aggregate**
  to `LawSection`s (structured; no regex). Network behind the interface; tests use a fixture.
- `Corpus/ILawCorpus.cs`, `Corpus/SqliteLawCorpus.cs` — versioned store + `Resolve(section, version)`.
- `Retrieval/ILawRetriever.cs` — `Search(queryCz, version, k)`; the pluggable seam.
- `Retrieval/FtsLawRetriever.cs` — SQLite FTS5 (`unicode61 remove_diacritics 2`, bm25).
- `LawTools.cs` — async `lookup_law` / `search_law` AI tools; year from active project.
- `Grounding/LawGroundingMiddleware.cs` — MAF middleware verifying citations in agent output.
- Numbers gate lives with Plan 2's calc: extend `ApprovalGate`/registration to require a resolvable
  `LawRef` in the pinned edition (see Task 7).

Reuse (do not rebuild): Plan 2 `ApprovalGate`, version pinning, and `Provenance.LawRef` (already
exists in `Core/Calc/Provenance.cs`); MAF middleware (GA as of Plan 2.5).

---

## 4. Task sequence

Each task is independently testable and offline (network import behind an interface, faked in tests).

### Task 1 — Scaffold `TaxClaw.Law`
`dotnet new classlib`, reference `TaxClaw.Core`, add `Microsoft.Extensions.AI`; add to solution.
Test project `TaxClaw.Law.Tests`. **Accept:** `dotnet build` green.

### Task 2 — Version, section, and version-set models
```csharp
public sealed record LawVersion(string ActNumber, DateOnly EffectiveOn)
{
    public string Eli => $"eli/cz/sb/{ActNumber.Split('/')[1]}/{ActNumber.Split('/')[0]}/{EffectiveOn:yyyy-MM-dd}";
}

public sealed record LawSection(
    string Section,        // "§ 6"
    string CitationLabel,  // official label, e.g. "§ 6 odst. 1" for a fragment; "§ 6" when aggregated
    string Text,           // aggregated, tag-stripped text of the section
    LawVersion Version,
    string SourceEli,
    string Hash);          // SHA-256 of Section+Text; pins wording

public sealed class LawVersionSet   // per project
{
    // edition per act, chosen for the project's tax year (see Open Decision D1)
    public LawVersion EditionFor(string actNumber) => ...;
}
```
**Tests:** ELI formatting; hash stable & wording-sensitive; `EditionFor` deterministic.

### Task 3 — e-Sbírka structured import (`ESbirkaSource : ILawSource`)
Pull an edition's fragments (SPARQL query in `tools/law-retrieval-eval/fetch_corpus.sh`, or the bulk
`003PravniAktZneniFragment` dataset) and **aggregate fragments to §-level `LawSection`s** by their
citation label's leading `§ N` (ordered by `pořadí`; strip `<var>`/tags). **No regex splitting.**
**Tests:** feed a fixture (a JSON slice of real fragments) → expected `LawSection`s with correct
`§`, aggregated text, and native citation. (The eval corpus JSON is a ready fixture source.)

### Task 4 — `ILawCorpus` + addressed lookup (`SqliteLawCorpus`)
Store `LawSection`s (SQLite), keyed by `(Section, Version)`. `Resolve(section, version)` returns the
exact §. This is the **primary** retrieval path (covers definitional §s the keyword search misses).
**Tests:** resolve `§ 6` for the 2027 edition; missing section → null; wrong edition → null.

### Task 5 — Keyword retriever (`FtsLawRetriever : ILawRetriever`)
SQLite FTS5 over `LawSection.Text`, `tokenize='unicode61 remove_diacritics 2'`, bm25 ranking, edition
-scoped. `Search(queryCz, version, k)` → `LawSearchResult(Section, Score)[]`. The query is expected in
Czech legal terms (raw EN measured at 0 recall). Vector deferred behind `ILawRetriever`.
**Tests:** mirror the eval — Czech query for "prodej cenných papírů" ranks `§ 10`/`§ 4`; empty query → [].

### Task 6 — Law tools (`lookup_law`, `search_law`) — async, project-scoped
```csharp
public sealed class LawTools(ILawCorpus corpus, ILawRetriever retriever, Func<LawVersion> activeEdition)
{
    [Description("Return the exact text and citation of a section for the active edition.")]
    public async Task<string> LookupLaw(string section) { ... resolve + format citation ... }

    [Description("Search Czech tax law (query MUST be in Czech legal terms) for the active edition.")]
    public async Task<string> SearchLaw(string queryCz) { ... top-N with citations ... }

    public IList<AITool> CreateTools() => [ AIFunctionFactory.Create(LookupLaw, "lookup_law"),
                                            AIFunctionFactory.Create(SearchLaw, "search_law") ];
}
```
Fixes from the review: **async** (no `.GetAwaiter().GetResult()`); **edition resolved dynamically**
via `activeEdition` (not baked in the constructor), matching runtime project switching.
**Tests:** lookup returns text + citation + act/edition; missing → "not found"; both tools exposed.

### Task 7 — Numbers grounding gate (extend Plan 2)
Make `Provenance.LawRef` **mandatory** for generated calc functions and pin the calc-function key to
the **law edition** (extend `CalcFunctionKey` beyond form version). At approval/registration, reject
a function whose `LawRef` does not `corpus.Resolve(...)` in the pinned edition.
**Tests:** a function citing a § absent from the edition is refused; a valid one is admitted; version
change re-triggers the check (ties into Plan 5 invalidation).

### Task 8 — Claims grounding middleware (MAF)
A MAF agent-run/output middleware: extract `§` citations from the answer; verify each resolves in the
pinned edition and the quoted text matches the corpus (hash/substring); ungrounded legal claims →
flag for human confirmation (per spec HITL, not silent block); verified → citations appended.
**Tests (with a fake agent output):** bogus `§ 999` → flagged; valid `§ 6` quote → passes; a numeric
claim with no citation on a legal question → flagged.

### Task 9 — Wire into the TUI agent + project pinning
Project stores a `LawVersionSet` (edition by tax year). On project open, load the corpus for that
edition; add `LawTools.CreateTools()` to the agent's tool list (auto-approved on Copilot via the
custom-tool permission handler from Plan 2.5); install the claims middleware.
**Accept:** `dotnet build` + `dotnet test` green; agent can `lookup_law("§ 6")` and cite it.

---

## 5. Open decisions

- **D1 — edition semantics.** Which edition is "in force" for a return for tax year Y: the one
  governing Y (default: **effective on 31.12.Y**) or the one in force at filing (Y+1)? e-Sbírka gives
  date-addressed editions, so either is a clean URI pick. **Default assumed: tax-year edition**;
  confirm before Task 9.
- **D2 — treaty & methodics.** The US–CZ double-taxation treaty (needed for §38f zápočet) and GFŘ
  methodics are **not** §-structured acts and live in a different collection (Sb. m. s.). Handle as a
  **follow-up** (separate `ILawSource` + citation shape); v1 grounds against 586/1992.
- **D3 — vector (deferred).** If discovery on definitional queries must harden, run the targeted
  vector experiment (`tools/law-retrieval-eval/`); needs an embedding provider — the default Copilot
  provider likely has none, so this would use a local/OpenAI embedding.

---

## 6. Sequencing & dependencies

- **No longer blocked on Plan 4.** Because e-Sbírka is machine-readable, importing the act does not
  need Plan 4's PDF/HTML extraction. (Plan 4 still handles user-supplied documents.)
- **Builds on Plan 2** (approval gate, version pinning, `Provenance.LawRef`) and **Plan 2.5** (MAF
  middleware + Copilot tool-calling).
- **Feeds Plan 5** (version-invalidation) and **Plan 7** (citations in exports).
