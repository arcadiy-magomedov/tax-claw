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

The Copilot provider uses the official [`GitHub.Copilot.SDK`](https://github.com/github/copilot-sdk),
which bundles the Copilot CLI runtime; it requires a GitHub Copilot subscription. Auth is resolved in
order: `GITHUB_COPILOT_TOKEN` env var → `gh auth token` → the logged-in Copilot user.

### Overriding the provider (optional)

Other providers (`ollama`, `openai`, `azure`) are available via `TAXCLAW_Llm__*` environment variables:

```bash
# one-shot smoke test against a different model (no interactive prompts)
dotnet run --project src/TaxClaw.Tui -- --ask "How are RSUs taxed in Czechia?"

# example: switch provider for a run
TAXCLAW_Llm__Provider=ollama TAXCLAW_Llm__Model=llama3.1 dotnet run --project src/TaxClaw.Tui
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
