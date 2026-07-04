# tax-claw

An LLM-agent TUI that helps prepare a Czech personal income tax declaration
(form 25 5405). The agent orchestrates; deterministic code computes — the model
never does floating-point arithmetic itself.

See the design spec in [`docs/superpowers/specs/2026-06-15-tax-claw-design.md`](docs/superpowers/specs/2026-06-15-tax-claw-design.md).

## Requirements
- .NET 10 SDK
- (Optional, for chat) a running [Ollama](https://ollama.com) with a model pulled, e.g. `ollama pull llama3.1`

## Install

Prebuilt, self-contained binaries (macOS, Linux, Windows; no .NET runtime required) are published to
[GitHub Releases](https://github.com/arcadiy-magomedov/tax-claw/releases) on every version tag.

macOS / Linux:

```bash
curl -fsSL https://raw.githubusercontent.com/arcadiy-magomedov/tax-claw/main/scripts/install.sh | sh
taxclaw
```

Windows: download `tax-claw-<version>-win-x64.zip` (or `win-arm64`) from the
[latest release](https://github.com/arcadiy-magomedov/tax-claw/releases/latest), extract it anywhere,
and run `taxclaw.exe`.

## Run from source

```bash
dotnet run --project src/TaxClaw.Tui
```

The interactive prompts require a real terminal.

## LLM provider & models

No config files. tax-claw defaults to **GitHub Copilot** with `claude-opus-4.8`, authenticated via the
GitHub CLI — just run `gh auth login` once. Change the model at runtime in the TUI with the
`/model` command:

- `/model` — show the current model and list available models
- `/model <id>` — switch model (conversation context is preserved), e.g. `/model gpt-5.5`

The interactive `/model` picker shows each model's context-window size and, for models that support
**thinking effort** (reasoning), prompts you to choose a level (`low`/`medium`/`high`/`xhigh`, or the
model default) after selection. Context-window size is a fixed model capability shown for reference.

Your model and thinking-effort choice is **remembered across runs** — it's saved to
`~/.tax-claw/preferences.json` whenever you switch with `/model`, and restored on the next launch.
Precedence at startup is: code defaults → saved preferences → `TAXCLAW_Llm__*` env vars (env wins,
so an env override applies for that run without changing your saved preference).

The agent is built on the **[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/)**
(`Microsoft.Agents.AI`, GA) over a provider-agnostic `IChatClient`. The Copilot provider uses the
official [`Microsoft.Agents.AI.GitHub.Copilot`](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot)
package (which bundles the Copilot CLI runtime) so tax-claw's own tools (decimal math, …) run through
the agent's function-calling; built-in Copilot CLI capabilities (shell, file, url) stay disabled. It
requires a GitHub Copilot subscription. Auth is resolved in order: `GITHUB_COPILOT_TOKEN` env var →
`gh auth token` → the logged-in Copilot user.

### Overriding the provider (optional)

Other providers (`ollama`, `openai`, `azure`) are available via `TAXCLAW_Llm__*` environment variables:

```bash
# one-shot smoke test against a different model (no interactive prompts)
dotnet run --project src/TaxClaw.Tui -- --ask "How are RSUs taxed in Czechia?"

# example: switch provider for a run
TAXCLAW_Llm__Provider=ollama TAXCLAW_Llm__Model=llama3.1 dotnet run --project src/TaxClaw.Tui
```

## Law grounding

The agent's tax reasoning is grounded in the **in-force Czech Income Tax Act (586/1992)**, imported
from the official **[e-Sbírka](https://e-sbirka.gov.cz) open data** (public-domain, structured by §).
Load the legislation for a tax year in the TUI:

```
/law 2027
```

This pins the edition in force for that year and gives the agent two tools:

- `lookup_law <§>` — exact text + citation of a section (the primary, addressed path);
- `search_law <czech query>` — keyword search (FTS5) for discovery; the query must be in Czech
  legal terms.

Every answer's `§` citations are checked against the loaded edition; citations that don't resolve
are flagged for you to verify (they are never silently trusted). See the retrieval measurement in
[`tools/law-retrieval-eval/`](tools/law-retrieval-eval/).

**Treaties (double taxation).** For foreign income (US RSUs/dividends) the relevant rules also live
in a double-taxation treaty (e.g. the US–CZ treaty, 32/1994). The importer aggregates treaty text by
**article** (`Čl. N`) as well as by `§`, because e-Sbírka cites treaty fragments as
`"Příloha  Čl. N …"`. Note that treaties carry only the sentinel `0000-00-00` edition (no dated
consolidations), so treaty loading must select that edition rather than the tax-year edition used for
acts — wiring that edition selection + the `/law` treaty command is the remaining step.

The core computation itself is modeled as a **grounded DAG** of form 25 5405 (see
`Form255405`): each line (partial bases § 6–§ 10 → base § 5 → tax § 16 → foreign-tax credit § 38f →
credits § 35ba/§ 35c → final liability) carries the provision that grounds it. The DAG encodes only
structure; the per-year arithmetic lives in the agent-generated, user-approved, version-pinned calc
functions, so a new year's rate never applies silently.

## Documents

Add a tax document to the active project's return:

```
/new 2027
/doc ~/statements/dividend.txt
```

The pipeline classifies the document, extracts entities **against a per-type schema** (only declared
fields are kept, so document text can't act as instructions), validates required fields, and maps
valid results to canonical income items with document provenance. Missing fields are surfaced for
you to confirm rather than guessed. Supported types: employment income, RSU vesting, dividends, and
brokerage trade confirmations (a SELL becomes a §10 disposal). Amounts keep their source currency;
FX conversion is a later calc step.

Classification and extraction run **deterministic-first**: a cheap keyword classifier and a
`label: value` extractor handle clean documents with reproducible, traceable results, and the
LLM is consulted only for ambiguous documents or missing required fields (its values never overwrite
deterministic ones — they only fill gaps). Text and CSV exports are read directly; scans, image-PDFs,
and photos fall back to a **Vision-LLM recognizer**. The LLM client is per-provider and PII-redacted,
and is created lazily so launch never pays for — or fails on — a provider you don't use for documents.

## Memory

The agent has durable, scoped memory under `~/.tax-claw/memory/`. Corrections you give are captured
via a `remember_feedback` tool and **outrank the agent's defaults** on later turns (e.g. "treat
Microsoft RSUs as § 6"). Memory is scoped — global, per-project, or per-document-type — and the
relevant entries are injected into each turn **MAF-natively**: the agent pulls remembered context
itself (via `AIAgentBuilder.UseAIContextProviders`), so injection is uniform across every provider —
including GitHub Copilot — and decoupled from the TUI loop. Learned artifacts (generated calc
functions, document parsers) are pinned to the law/form version they were derived against and
**invalidated when that version changes**, so last year's rule never silently applies to a new year.

## Skills & sharing

Know-how is packageable as a **skill / knowledge pack** — a manifest (id, version, pinned law/form
version, author, content hash) plus generalized artifacts (format parsers, calc functions, mapping
rules), under `~/.tax-claw/skills/`. Packs are shared as plain files via a git repo (PR review),
not an in-app registry.

- **Export** bundles artifacts only after a PII scan clears them — PII can't leave by construction
  (documents/amounts are never in shareable artifacts; the scanner is defense-in-depth).
- **Import** treats a foreign pack as untrusted: it verifies the content hash, refuses on PII, and
  stages into `skills-pending/` — **nothing activates or runs until you approve it** (reusing the
  calc approval gate + sandbox), and version pins keep another year's rule from applying silently.
- **MCP:** internal tools are published behind a Model Context Protocol surface for future sharing;
  transport hosting and external-server consumption (which the agent framework supports natively)
  land when needed — v1 has no external MCP servers by design.

## Privacy & exporters

**Privacy:** on the cloud path, a PII-redacting middleware pseudonymizes structured personal data
(rodné číslo, IBAN) before it leaves and restores it in the reply; local providers (Ollama) are
never wrapped. Toggle with `TAXCLAW_Llm__RedactPii` (default on). Regex redaction is best-effort
(structured IDs, not free-text names) — the strong guarantee is local mode. Redaction runs at the
**agent-run boundary**, so it covers every provider uniformly, including the default GitHub Copilot
path (which is reached through the agent-framework provider, not an `IChatClient`).

**Exporters** project the canonical return; each is a milestone behind one seam:

```
/export summary ~/out/2027.md     # markdown with per-line traces + § citations
/export pdf     ~/out/2027.pdf     # form 25 5405 (QuestPDF)
/export xml     ~/out/2027.xml     # portal XML, validated against the EPO XSD
```

The official EPO schema for form 25 5405 (`dpfdp5_epo2.xsd`) is published — and versioned per tax
year — on the MOJE daně documentation portal ("Popis struktury souborů"). Drop the downloaded file
at `~/.tax-claw/schemas/dpfdp5_epo2.xsd` and `/export xml` validates against it automatically; absent
that, a built-in stand-in validates the current interim shape. The official schema's real element
structure is `Pisemnost → DPFDP5 → Veta_*`, which the exporter emits once the real form-line model
lands — until then, validating the interim shape against the official XSD is expected to fail.

## Test

```bash
dotnet test
```

## Package management

This repo uses **Central Package Management** — all package versions live in
[`Directory.Packages.props`](Directory.Packages.props), including transitive pinning
(`CentralPackageTransitivePinningEnabled`). NuGet lock files (`packages.lock.json`) are committed for
reproducible restores.

## Releases

Pushing a tag matching `v*.*.*` triggers [`.github/workflows/release.yml`](.github/workflows/release.yml):
it runs the full test suite, then publishes self-contained single-file binaries for
`osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64`, and `win-arm64`, and attaches them
(plus `SHA256SUMS` and the install script) to a new GitHub Release.

```bash
git tag v0.1.0
git push origin v0.1.0
```

CI (`.github/workflows/ci.yml`) runs on every push/PR to `main`: restore (with known-vulnerability
advisories escalated to build errors), build, and test.

## Data location

Projects and profile are stored under `~/.tax-claw/`.
