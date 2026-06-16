using System.Text;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

/// <summary>
/// An <see cref="IChatClient"/> backed by the official GitHub Copilot SDK. Routes prompts to a
/// Copilot model (e.g. "claude-opus-4.8") via the bundled Copilot CLI runtime. Authentication uses
/// the supplied GitHub token, or the logged-in Copilot user when no token is given.
/// </summary>
/// <remarks>
/// Tool use inside the Copilot runtime is denied so the model behaves as a plain chat model —
/// tax-claw's own tools (decimal math, law lookup, etc.) run on the .NET side, not via the CLI.
/// Each call creates a fresh, stateless session seeded with the system prompt and the conversation
/// transcript, so the provider is safe to use behind tax-claw's own conversation history.
/// </remarks>
public sealed class CopilotChatClient : IChatClient
{
    private readonly string _model;
    private readonly string? _gitHubToken;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private CopilotClient? _client;
    private bool _disposed;

    public CopilotChatClient(string model, string? gitHubToken = null)
    {
        _model = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("A Copilot model id is required.", nameof(model))
            : model;
        _gitHubToken = string.IsNullOrWhiteSpace(gitHubToken) ? null : gitHubToken;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var all = messages.ToList();
        string? system = JoinText(all.Where(m => m.Role == ChatRole.System));
        string prompt = BuildPrompt(all.Where(m => m.Role != ChatRole.System).ToList());

        CopilotClient client = await EnsureStartedAsync(cancellationToken);

        var config = new SessionConfig
        {
            Model = _model,
            OnPermissionRequest = DenyAllTools
        };
        if (!string.IsNullOrWhiteSpace(system))
        {
            config.SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = system
            };
        }

        await using CopilotSession session = await client.CreateSessionAsync(config, cancellationToken);
        AssistantMessageEvent? final = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt }, timeout: null, cancellationToken);

        string text = final?.Data?.Content ?? string.Empty;
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Minimal streaming: produce the full response as a single update. The TUI agent uses the
        // non-streaming path; richer token streaming can layer on session delta events later.
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType?.IsInstanceOfType(this) == true ? this : null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _client?.Dispose();
        _startGate.Dispose();
    }

    private async Task<CopilotClient> EnsureStartedAsync(CancellationToken ct)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _startGate.WaitAsync(ct);
        try
        {
            if (_client is null)
            {
                var options = new CopilotClientOptions();
                if (_gitHubToken is not null)
                {
                    options.GitHubToken = _gitHubToken;
                }

                var client = new CopilotClient(options);
                await client.StartAsync(ct);
                _client = client;
            }
        }
        finally
        {
            _startGate.Release();
        }

        return _client;
    }

    // PermissionDecision is marked [Experimental(GHCP001)] in the SDK; Reject(...) is the supported
    // way to deny a tool call, so the experimental diagnostic is suppressed narrowly here.
#pragma warning disable GHCP001
    private static Task<PermissionDecision> DenyAllTools(PermissionRequest request, PermissionInvocation invocation) =>
        Task.FromResult(PermissionDecision.Reject("Tool use is disabled in the tax-claw Copilot provider."));
#pragma warning restore GHCP001

    private static string? JoinText(IEnumerable<ChatMessage> messages)
    {
        string joined = string.Join("\n\n", messages.Select(m => m.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        return joined.Length == 0 ? null : joined;
    }

    private static string BuildPrompt(IReadOnlyList<ChatMessage> conversation)
    {
        if (conversation.Count == 0)
        {
            return string.Empty;
        }
        if (conversation.Count == 1)
        {
            return conversation[0].Text;
        }

        var sb = new StringBuilder();
        foreach (ChatMessage message in conversation)
        {
            sb.Append(message.Role.Value).Append(": ").AppendLine(message.Text).AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
