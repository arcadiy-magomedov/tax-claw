using TaxClaw.Law.Model;

namespace TaxClaw.Law.Grounding;

/// <summary>The outcome of checking an agent answer's § citations against the pinned edition.</summary>
public sealed record GroundingReport(IReadOnlyList<string> UnresolvedCitations)
{
    /// <summary>True when every § the answer cites resolves in the edition (nothing to flag).</summary>
    public bool IsGrounded => UnresolvedCitations.Count == 0;
}

/// <summary>
/// Verifies that every § an agent answer cites actually resolves in the project's pinned edition —
/// catching hallucinated or stale citations. Ungrounded citations are surfaced for human
/// confirmation (per the spec's human-in-the-loop stance), not silently blocked.
/// </summary>
public sealed class ClaimsGroundingChecker(LawGroundingGate gate)
{
    public GroundingReport Verify(string answer, LawVersion edition)
    {
        var unresolved = gate.References(answer)
            .Where(section => !gate.IsGrounded(section, edition))
            .ToList();
        return new GroundingReport(unresolved);
    }

    /// <summary>Returns the answer unchanged when grounded; otherwise appends a verification note.</summary>
    public string Annotate(string answer, LawVersion edition)
    {
        GroundingReport report = Verify(answer, edition);
        if (report.IsGrounded)
        {
            return answer;
        }
        return $"{answer}\n\n⚠ Grounding check: these citations were not found in the in-force edition "
            + $"({edition.Eli}): {string.Join(", ", report.UnresolvedCitations)}. "
            + "Please verify against the law before relying on this.";
    }
}
