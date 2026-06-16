using Microsoft.Extensions.AI;
using TaxClaw.Llm;
using Xunit;

namespace TaxClaw.Llm.Tests;

public class ChatClientFactoryTests
{
    [Fact]
    public void Ollama_provider_builds_a_chat_client_without_network()
    {
        var options = new LlmOptions { Provider = "ollama", Model = "llama3.1" };
        IChatClient client = new ChatClientFactory(options).Create();
        Assert.NotNull(client);
    }

    [Fact]
    public void Unknown_provider_throws()
    {
        var options = new LlmOptions { Provider = "does-not-exist", Model = "x" };
        Assert.Throws<NotSupportedException>(() => new ChatClientFactory(options).Create());
    }

    [Fact]
    public void Azure_provider_requires_an_endpoint()
    {
        var options = new LlmOptions { Provider = "azure", Model = "gpt-4o", ApiKey = "k" };
        Assert.Throws<ArgumentException>(() => new ChatClientFactory(options).Create());
    }

    [Fact]
    public void OpenAi_provider_requires_an_api_key()
    {
        var options = new LlmOptions { Provider = "openai", Model = "gpt-4o" };
        Assert.Throws<ArgumentException>(() => new ChatClientFactory(options).Create());
    }

    [Fact]
    public void Copilot_provider_builds_a_chat_client()
    {
        var options = new LlmOptions { Provider = "copilot", Model = "claude-opus-4.8", ApiKey = "token" };
        IChatClient client = new ChatClientFactory(options).Create();
        Assert.IsType<CopilotChatClient>(client);
    }
}
