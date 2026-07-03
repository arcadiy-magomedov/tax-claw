using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TaxClaw.Llm;
using Xunit;

namespace TaxClaw.Llm.Tests;

public class AgentFactoryTests
{
    private static readonly IList<AITool> NoTools = [];

    [Fact]
    public void Ollama_provider_builds_an_agent_without_network()
    {
        var options = new LlmOptions { Provider = "ollama", Model = "llama3.1" };
        AIAgent agent = new AgentFactory(options).CreateAgent("system", NoTools);
        Assert.NotNull(agent);
    }

    [Fact]
    public void Unknown_provider_throws()
    {
        var options = new LlmOptions { Provider = "does-not-exist", Model = "x" };
        Assert.Throws<NotSupportedException>(() => new AgentFactory(options).CreateAgent("system", NoTools));
    }

    [Fact]
    public void Azure_provider_requires_an_endpoint()
    {
        var options = new LlmOptions { Provider = "azure", Model = "gpt-4o", ApiKey = "k" };
        Assert.Throws<ArgumentException>(() => new AgentFactory(options).CreateAgent("system", NoTools));
    }

    [Fact]
    public void OpenAi_provider_requires_an_api_key()
    {
        var options = new LlmOptions { Provider = "openai", Model = "gpt-4o" };
        Assert.Throws<ArgumentException>(() => new AgentFactory(options).CreateAgent("system", NoTools));
    }

    [Fact]
    public void Copilot_provider_builds_an_agent()
    {
        var options = new LlmOptions { Provider = "copilot", Model = "claude-opus-4.8", ApiKey = "token" };
        AIAgent agent = new AgentFactory(options).CreateAgent("system", NoTools);
        Assert.NotNull(agent);
    }
}
