namespace TaxClaw.Memory;

public enum ScopeKind { Global, Project, DocumentType }

/// <summary>
/// Where a memory applies. Global memory is always in play; project- and doc-type-scoped memory
/// only surfaces in matching contexts. This is how "re-confirm per project" and targeted feedback
/// are expressed.
/// </summary>
public sealed record MemoryScope(ScopeKind Kind, string? Key)
{
    public static MemoryScope Global() => new(ScopeKind.Global, null);
    public static MemoryScope Project(string projectId) => new(ScopeKind.Project, projectId);
    public static MemoryScope DocumentType(string documentType) => new(ScopeKind.DocumentType, documentType);

    public bool IsRelevantTo(string? projectId, string? documentType) => Kind switch
    {
        ScopeKind.Global => true,
        ScopeKind.Project => Key == projectId,
        ScopeKind.DocumentType => Key == documentType,
        _ => false
    };
}
