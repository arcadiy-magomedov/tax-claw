using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace TaxClaw.Memory;

/// <summary>
/// Lets the agent persist a user correction as feedback memory. Scoped to a document type when
/// given, else to the active project (resolved dynamically, so switching projects re-scopes), else
/// global. Feedback outranks the agent's defaults on later turns.
/// </summary>
public sealed class FeedbackTools(IMemoryStore store, Func<string?> activeProjectId)
{
    [Description("Remember a user correction so future runs honor it. Optionally scope it to a "
        + "document type (e.g. 'rsu_vesting'); otherwise it applies to the active project.")]
    public async Task<string> RememberFeedback(
        [Description("the correction to remember")] string correction,
        [Description("optional document type to scope the correction to")] string? documentType = null)
    {
        MemoryScope scope = !string.IsNullOrWhiteSpace(documentType)
            ? MemoryScope.DocumentType(documentType)
            : activeProjectId() is { } projectId
                ? MemoryScope.Project(projectId)
                : MemoryScope.Global();

        await store.AddAsync(new MemoryEntry(
            Guid.NewGuid().ToString("N"), MemoryKind.Feedback, scope, correction, DateTimeOffset.UtcNow));

        return "Got it — remembered.";
    }

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(RememberFeedback, name: "remember_feedback")
    ];
}
