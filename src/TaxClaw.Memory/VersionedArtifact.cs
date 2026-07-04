namespace TaxClaw.Memory;

public enum ArtifactKind { CalcFunction, DocumentParser }

/// <summary>
/// A learned, reusable artifact pinned to the law (and, for calc functions, form) version it was
/// derived against. The pin is what lets us invalidate last year's rule when versions change.
/// </summary>
public sealed record VersionedArtifact(
    string Id,
    ArtifactKind Kind,
    string LawVersion,
    string? FormVersion);
