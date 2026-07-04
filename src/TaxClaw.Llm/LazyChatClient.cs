using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

/// <summary>
/// Defers creation of the underlying <see cref="IChatClient"/> until the first call. The document
/// pipeline is wired at startup, but building a provider client (especially the Copilot agent, which
/// resolves a token and opens an RPC connection) should only happen when a document is actually
/// processed — not on every launch, and never failing startup for users who never process documents.
/// </summary>
public sealed class LazyChatClient(Func<IChatClient> factory) : IChatClient
{
    private IChatClient? _inner;

    private IChatClient Inner => _inner ??= factory();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Inner.GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) =>
        Inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    // Do not force creation just to probe services.
    public object? GetService(Type serviceType, object? serviceKey = null) => _inner?.GetService(serviceType, serviceKey);

    public void Dispose() => _inner?.Dispose();
}
