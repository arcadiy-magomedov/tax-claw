using Microsoft.Extensions.Configuration;
using TaxClaw.Agent;
using TaxClaw.Llm;
using TaxClaw.Storage;
using TaxClaw.Tui;

// Configuration is code-default first (GitHub Copilot / claude-opus-4.8), with optional env-var
// overrides (e.g. TAXCLAW_Llm__Provider=openai). No config files — models are chosen at runtime
// with the /model command.
IConfiguration config = new ConfigurationBuilder()
    .AddEnvironmentVariables(prefix: "TAXCLAW_")
    .Build();

var llmOptions = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();

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

var root = new StorageRoot();
var profiles = new JsonProfileStore(root);
var projects = new JsonProjectStore(root);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await new AppHost(agent, profiles, projects, llmOptions, factory.Create, factory.CreateCatalog()).RunAsync(cts.Token);
