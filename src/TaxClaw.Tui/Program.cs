using Microsoft.Extensions.Configuration;
using TaxClaw.Agent;
using TaxClaw.Core.Model;
using TaxClaw.Llm;
using TaxClaw.Storage;
using TaxClaw.Tui;

// Precedence: code defaults → saved preferences (~/.tax-claw/preferences.json) → env-var overrides.
// No config files; the model/effort are chosen at runtime with /model and persisted across runs.
var root = new StorageRoot();
var preferencesStore = new JsonPreferencesStore(root);

var llmOptions = new LlmOptions();
Preferences? savedPreferences = await preferencesStore.LoadAsync();
if (savedPreferences is not null)
{
    if (!string.IsNullOrWhiteSpace(savedPreferences.Provider)) llmOptions.Provider = savedPreferences.Provider;
    if (!string.IsNullOrWhiteSpace(savedPreferences.Model)) llmOptions.Model = savedPreferences.Model;
    llmOptions.ReasoningEffort = savedPreferences.ReasoningEffort;
}

// Env vars (TAXCLAW_Llm__*) override saved preferences and code defaults.
IConfiguration config = new ConfigurationBuilder()
    .AddEnvironmentVariables(prefix: "TAXCLAW_")
    .Build();
config.GetSection("Llm").Bind(llmOptions);

var factory = new ChatClientFactory(llmOptions);
var agent = new TaxClawAgent(factory.Create(), Prompts.System, MathTools.CreateTools());

// Non-interactive smoke test: `--ask "<prompt>"` sends one message and prints the reply.
// Useful for validating an LLM provider (e.g. Copilot) without the interactive TUI prompts.
int askIndex = Array.IndexOf(args, "--ask");
if (askIndex >= 0 && askIndex + 1 < args.Length)
{
    string reply = await agent.SendAsync(args[askIndex + 1]);
    Console.WriteLine(reply);
    return;
}

var profiles = new JsonProfileStore(root);
var projects = new JsonProjectStore(root);

// Persist the model/effort whenever it changes via /model, so it survives restarts.
async Task PersistPreferencesAsync(CancellationToken ct) =>
    await preferencesStore.SaveAsync(
        new Preferences
        {
            Provider = llmOptions.Provider,
            Model = llmOptions.Model,
            ReasoningEffort = llmOptions.ReasoningEffort
        }, ct);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await new AppHost(
    agent, profiles, projects, llmOptions,
    factory.Create, factory.CreateCatalog(), PersistPreferencesAsync).RunAsync(cts.Token);
