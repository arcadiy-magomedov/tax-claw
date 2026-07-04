namespace TaxClaw.Memory;

/// <summary>The kind of remembered item. Priority orders how strongly it overrides defaults.</summary>
public enum MemoryKind { Preference, Fact, Feedback }

public static class MemoryKindExtensions
{
    /// <summary>Higher wins: user feedback/corrections outrank facts, which outrank preferences.</summary>
    public static int Priority(this MemoryKind kind) => kind switch
    {
        MemoryKind.Feedback => 3,
        MemoryKind.Fact => 2,
        MemoryKind.Preference => 1,
        _ => 0
    };
}

/// <summary>A single remembered item with its scope and provenance timestamp.</summary>
public sealed record MemoryEntry(
    string Id,
    MemoryKind Kind,
    MemoryScope Scope,
    string Text,
    DateTimeOffset CreatedAt);
