namespace TaxClaw.Tui;

/// <summary>
/// Mutable per-run state shared between the composition-built agent tools (which capture it via a
/// delegate) and the <see cref="AppHost"/> loop that updates it — e.g. the active project id used
/// for memory scoping.
/// </summary>
public sealed class SessionState
{
    public string? ActiveProjectId { get; set; }
}
