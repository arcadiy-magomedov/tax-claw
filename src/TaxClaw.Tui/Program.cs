using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using TaxClaw.Agent;
using TaxClaw.Core.Model;
using TaxClaw.Documents;
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Extract;
using TaxClaw.Law;
using TaxClaw.Law.Ingest;
using TaxClaw.Llm;
using TaxClaw.Memory;
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

var factory = new AgentFactory(llmOptions);

// Law grounding: the session starts empty and is populated by /law <year>; its tools (lookup_law,
// search_law) and claims checker bind to it once and follow the active edition.
var lawSession = new LawSession();
using var httpClient = new HttpClient();
ILawSource lawSource = ESbirkaSource.Http(httpClient);

// Durable memory: scoped facts + user corrections injected each turn; remember_feedback lets the
// agent persist a correction. The active project id (for scoping) is shared via SessionState.
var memoryStore = new JsonMemoryStore(root);
var memoryContext = new MemoryContextProvider(memoryStore);
var sessionState = new SessionState();
var feedbackTools = new FeedbackTools(memoryStore, () => sessionState.ActiveProjectId);

var tools = MathTools.CreateTools()
    .Concat(lawSession.Tools.CreateTools())
    .Concat(feedbackTools.CreateTools())
    .ToList();
AIAgent BuildAgent() => factory.CreateAgent(Prompts.System, tools);
await using var agent = new TaxClawAgent(BuildAgent());

// Document pipeline: a text layer is preferred; scans/image-PDFs fall back to a Vision-LLM
// recognizer. Classification and schema-bound extraction run deterministic-first, consulting the
// LLM only for ambiguous documents / missing required fields. The LLM client is created lazily
// (per-provider, PII-redacted) so launch never pays for — or fails on — an unused provider.
var llmDocClient = new LazyChatClient(factory.CreateChatClient);
var documentPipeline = new DocumentPipeline(
    new TextLayerDetector(new PlainTextExtractor(), new VisionRecognizer(llmDocClient)),
    new FallbackClassifier(new KeywordClassifier(), new LlmDocumentClassifier(llmDocClient)),
    new FallbackEntityExtractor(new LabelledLineExtractor(), new LlmEntityExtractor(llmDocClient)));

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
    BuildAgent, lawSession, lawSource, documentPipeline, memoryContext, sessionState,
    factory.CreateCatalog(), PersistPreferencesAsync).RunAsync(cts.Token);
