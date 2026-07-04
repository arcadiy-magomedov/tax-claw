using System.Text.RegularExpressions;
using TaxClaw.Law.Corpus;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Grounding;

/// <summary>Raised when a rule/claim cites law that does not resolve in the pinned edition.</summary>
public sealed class UngroundedLawReferenceException(string message) : Exception(message);

/// <summary>
/// Enforces the grounding invariant: a citation is valid only if every § it references resolves in
/// the project's pinned edition. Used at calc-approval time (numbers gate) and by the claims
/// middleware. Empty or unresolved references are rejected — a figure/claim must trace to in-force law.
/// </summary>
public sealed partial class LawGroundingGate(ILawCorpus corpus)
{
    /// <summary>The § references found in a citation string, in first-seen order.</summary>
    public IReadOnlyList<string> References(string? citation)
    {
        if (string.IsNullOrWhiteSpace(citation))
        {
            return [];
        }
        var seen = new List<string>();
        foreach (Match m in SectionRef().Matches(citation))
        {
            string s = $"§ {m.Groups[1].Value}";
            if (!seen.Contains(s))
            {
                seen.Add(s);
            }
        }
        return seen;
    }

    /// <summary>True iff the citation names at least one § and every named § resolves in the edition.</summary>
    public bool IsGrounded(string? citation, LawVersion edition)
    {
        IReadOnlyList<string> refs = References(citation);
        return refs.Count > 0 && refs.All(s => corpus.Resolve(s, edition) is not null);
    }

    public void EnsureGrounded(string? citation, LawVersion edition)
    {
        if (!IsGrounded(citation, edition))
        {
            throw new UngroundedLawReferenceException(
                $"Law reference '{citation ?? "(none)"}' does not resolve in edition {edition.Eli}; "
                + "a calc rule must cite an in-force section.");
        }
    }

    [GeneratedRegex(@"§\s*(\d+[a-z]*)", RegexOptions.CultureInvariant)]
    private static partial Regex SectionRef();
}
