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

Text and CSV exports are read directly; scans/PDFs/images need the OCR/Vision recognizer, which is a
deferred adapter (lands with the privacy-aware recognizer). LLM-backed classification/extraction
implement the same seams for messy real-world formats.

## Memory

The agent has durable, scoped memory under `~/.tax-claw/memory/`. Corrections you give are captured
via a `remember_feedback` tool and **outrank the agent's defaults** on later turns (e.g. "treat
Microsoft RSUs as § 6"). Memory is scoped — global, per-project, or per-document-type — and the
relevant entries are injected into each turn. Learned artifacts (generated calc functions, document
parsers) are pinned to the law/form version they were derived against and **invalidated when that
version changes**, so last year's rule never silently applies to a new year.

## Test

```bash
dotnet test
```

## Package management

This repo uses **Central Package Management** — all package versions live in
[`Directory.Packages.props`](Directory.Packages.props). NuGet lock files
(`packages.lock.json`) are committed for reproducible restores.

## Data location

Projects and profile are stored under `~/.tax-claw/`.
