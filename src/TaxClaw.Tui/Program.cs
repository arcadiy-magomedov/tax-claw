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

var root = new StorageRoot();
var profiles = new JsonProfileStore(root);
var projects = new JsonProjectStore(root);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await new AppHost(agent, profiles, projects).RunAsync(cts.Token);
