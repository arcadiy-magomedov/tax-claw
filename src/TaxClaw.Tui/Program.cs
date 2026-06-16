using Microsoft.Extensions.Configuration;
using TaxClaw.Agent;
using TaxClaw.Llm;
using TaxClaw.Storage;
using TaxClaw.Tui;

IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables(prefix: "TAXCLAW_")
    .Build();

var llmOptions = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();

var chatClient = new ChatClientFactory(llmOptions).Create();
var agent = new TaxClawAgent(chatClient, Prompts.System, MathTools.CreateTools());

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

await new AppHost(agent, profiles, projects).RunAsync(cts.Token);
