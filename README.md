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

## Configure the LLM provider

Edit `src/TaxClaw.Tui/appsettings.json` (or set `TAXCLAW_Llm__*` env vars):

```json
{ "Llm": { "Provider": "ollama", "Model": "llama3.1" } }
```

Supported providers: `ollama`, `openai` (needs `ApiKey`), `azure` (needs `Endpoint` + `ApiKey`), `copilot` (GitHub Copilot models).

### GitHub Copilot provider

Routes to Copilot models (e.g. `claude-opus-4.8`, `gpt-5.5`) via the official
[`GitHub.Copilot.SDK`](https://github.com/github/copilot-sdk), which bundles the Copilot CLI runtime.
Requires a GitHub Copilot subscription. Authentication is resolved in order:
`ApiKey` → `GITHUB_COPILOT_TOKEN` env var → `gh auth token` → the logged-in Copilot user.

```bash
# one-shot smoke test against Opus 4.8 (no interactive prompts)
TAXCLAW_Llm__Provider=copilot TAXCLAW_Llm__Model=claude-opus-4.8 \
  GITHUB_COPILOT_TOKEN="$(gh auth token)" \
  dotnet run --project src/TaxClaw.Tui -- --ask "How are RSUs taxed in Czechia?"
```

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
