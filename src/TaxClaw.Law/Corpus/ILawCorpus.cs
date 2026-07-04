using TaxClaw.Law.Model;

namespace TaxClaw.Law.Corpus;

/// <summary>
/// The versioned legislation store. Holds sections across editions and serves the primary
/// grounding path: an exact, addressed lookup of a § in a specific edition.
/// </summary>
public interface ILawCorpus
{
    /// <summary>Adds or replaces the given sections (typically all sections of one edition).</summary>
    Task IngestAsync(IEnumerable<LawSection> sections, CancellationToken ct = default);

    /// <summary>Returns the § as it stands in the given edition, or null if absent.</summary>
    LawSection? Resolve(string section, LawVersion version);
}
