# tax-claw — Implementation Plan Roadmap

The design spec ([../specs/2026-06-15-tax-claw-design.md](../specs/2026-06-15-tax-claw-design.md)) is decomposed
into seven plans. Each plan produces working, tested software on its own and builds on the ones
before it. Implement them in order.

## Sequence

| # | Plan | Builds on | Delivers |
|---|------|-----------|----------|
| 1 | [Foundation & walking skeleton](2026-06-15-tax-claw-foundation.md) | — | Runnable TUI: create per-year projects, cross-project profile, provider-agnostic chat with decimal-math tools |
| 2 | [Canonical model & calc runtime](2026-06-15-tax-claw-calc-runtime.md) | 1 | `TaxReturn`, form DAG, approved version-pinned generated functions, `CalculationTrace` |
| 3 | [Law corpus & RAG](2026-06-15-tax-claw-law-rag.md) | 1 | Versioned legislation, hybrid search, `lookup_law`/`search_law` with citations |
| 4 | [Document pipeline](2026-06-15-tax-claw-document-pipeline.md) | 1, 2 | Drop a doc → classify → OCR/text → schema-bound entities → mapped to the return |
| 5 | [Memory & feedback](2026-06-15-tax-claw-memory.md) | 1, 2, 4 | Scoped memory, prioritized feedback, version invalidation of learned artifacts |
| 6 | [Skills, MCP & sharing](2026-06-15-tax-claw-skills-sharing.md) | 1, 2, 5 | Skill/knowledge-pack format, loading, PII-gated export, safe import, MCP tool surface |
| 7 | [Privacy & exporters](2026-06-15-tax-claw-privacy-exporters.md) | 1, 2 | PII-redacting middleware on the cloud path; Summary → PDF → XML exporters |

## Dependency notes

- The `IChatClient` seam introduced in Plan 1 is the swap point for Plan 7's PII middleware and for
  a future Microsoft Agent Framework agent. Keeping it thin in Plan 1 is deliberate.
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
- **MAF agent upgrade** — replace the thin Plan 1 agent with the Microsoft Agent Framework agent
  (memory/MCP/middleware) via the same seam, after Plans 2–7 stabilize.
- **Full corpus/form ingestion wiring** — loading real legislation and the official form/instructions
  for the active year into the Plan 3 corpus and Plan 2 form DAG (the seams exist; data wiring is a
  composition step in the TUI).

## Open questions from spec §14 and where they resolve

- Default dev LLM provider → Plan 1 config (`appsettings.json`).
- EPO XSD source / XML shape → Plan 7, Task 7.
- Legislation source + update policy → Plan 3 ingestion.
- macOS sandbox technology → deferred hardening (above).
- Skill manifest schema → Plan 6, Task 2.
