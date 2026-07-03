# tax-claw — Implementation Plan Roadmap

The design spec ([../specs/2026-06-15-tax-claw-design.md](../specs/2026-06-15-tax-claw-design.md)) is decomposed
into seven plans. Each plan produces working, tested software on its own and builds on the ones
before it. Implement them in order.

> **Update 2026-07-03 — MAF adoption promoted.** The Microsoft Agent Framework
> (`Microsoft.Agents.AI`) went **GA on 2026-04-02** (now v1.13.0), so the original decision to defer
> the MAF upgrade until "after Plans 2–7" is revised. MAF is adopted **now, before Plan 3** (as
> **Plan 2.5** below), because Plans 5/6/7 planned to hand-roll features MAF ships GA (memory/context
> providers, MCP client + Agent Skills, and middleware for the PII boundary). Building them by hand
> and swapping later would be double work. See the spec §3 update note. Residual risk: MAF's fast
> minor-release cadence and the still-**RC** Copilot provider (`Microsoft.Agents.AI.GitHub.Copilot`
> `1.13.0-rc1`) — mitigated by version pinning.

## Sequence

| # | Plan | Builds on | Delivers |
|---|------|-----------|----------|
| 1 | [Foundation & walking skeleton](2026-06-15-tax-claw-foundation.md) | — | Runnable TUI: create per-year projects, cross-project profile, provider-agnostic chat with decimal-math tools |
| 2 | [Canonical model & calc runtime](2026-06-15-tax-claw-calc-runtime.md) | 1 | `TaxReturn`, form DAG, approved version-pinned generated functions, `CalculationTrace` |
| 2.5 | **MAF adoption** (promoted from Deferred, 2026-07-03) | 1 | Replace the thin `IChatClient` agent with a MAF `AIAgent` (`chatClient.AsAIAgent`); adopt the **official** `Microsoft.Agents.AI.GitHub.Copilot` provider so **function tools actually fire on the default Copilot provider** (the hand-rolled adapter did not bridge tools). Unlocks native memory, MCP, middleware for later plans. |
| 3 | [Law grounding & retrieval](2026-06-15-tax-claw-law-rag.md) *(revised 2026-07-03)* | 1, 2, 2.5 | **Grounding contract**: versioned e-Sbírka corpus (structured import, no parser), addressed `lookup_law(§)` + FTS5 keyword (Czech expansion) — vector deferred; approval-time gate on calc `LawRef` + MAF claims middleware so every decision cites in-force law |
| 4 | [Document pipeline](2026-06-15-tax-claw-document-pipeline.md) | 1, 2 | Drop a doc → classify → OCR/text → schema-bound entities → mapped to the return |
| 5 | [Memory & feedback](2026-06-15-tax-claw-memory.md) | 1, 2, 4 | Scoped memory, prioritized feedback, version invalidation of learned artifacts |
| 6 | [Skills, MCP & sharing](2026-06-15-tax-claw-skills-sharing.md) | 1, 2, 5 | Skill/knowledge-pack format, loading, PII-gated export, safe import, MCP tool surface |
| 7 | [Privacy & exporters](2026-06-15-tax-claw-privacy-exporters.md) | 1, 2 | PII-redacting middleware on the cloud path; Summary → PDF → XML exporters |

## Dependency notes

- The `IChatClient` seam introduced in Plan 1 is the swap point for Plan 7's PII middleware and for
  the Microsoft Agent Framework agent (adopted in Plan 2.5). Keeping it thin in Plan 1 was
  deliberate and paid off: MAF's `AIAgent` builds directly on top of any `IChatClient`.
- Plan 2's `ApprovalGate` + `ScriptCompiler` are reused by Plan 4 (parser generation) and Plan 6
  (importing foreign code). There is one approval/sandbox path, not three.
- Version pinning is shared: Plan 2 pins calc functions to law+form version; Plan 5's
  `ArtifactInvalidator` enforces it; Plan 6's `SkillManifest` carries the same pins.
- The PII scanner appears in both Plan 6 (pack export/import gate) and Plan 7 (cloud-path
  detector). They serve different boundaries; both are intentional defense-in-depth.

## Deferred (tracked, not lost)

- **At-rest encryption** (OS keychain) — small follow-up: an `IDataProtector` seam around the JSON
  stores from Plan 1. Noted at the end of Plan 7.
- **OS-level sandbox** — Plans 2/4 use an in-process timeout guard; a container/subprocess sandbox
  is a hardening pass once the generation paths are exercised.
- **MAF agent upgrade** — ~~replace the thin Plan 1 agent with the Microsoft Agent Framework agent
  (memory/MCP/middleware) via the same seam, after Plans 2–7 stabilize.~~ **Promoted to Plan 2.5
  (2026-07-03)** — MAF reached GA (2026-04-02), so this is no longer deferred; it runs before Plan 3
  so later plans can use MAF's native memory/MCP/middleware instead of hand-rolled equivalents.
- **Full corpus/form ingestion wiring** — the **legislation source is now resolved** (e-Sbírka open
  data; structured import, see revised Plan 3). Remaining: composing the import for the active
  project year, and loading the official form/instructions into the Plan 2 form DAG. Treaty (US–CZ)
  and GFŘ methodics are a Plan 3 follow-up (different collection/structure).

## Open questions from spec §14 and where they resolve

- Default dev LLM provider → Plan 1 config (`appsettings.json`).
- EPO XSD source / XML shape → Plan 7, Task 7.
- Legislation source + update policy → **RESOLVED (2026-07-03): official e-Sbírka open data**
  (`opendata.eselpoint.gov.cz`, SPARQL/bulk/REST; keyless, public-domain, daily-updated; date-
  addressed editions). See revised Plan 3 §1.
- macOS sandbox technology → deferred hardening (above).
- Skill manifest schema → Plan 6, Task 2.
