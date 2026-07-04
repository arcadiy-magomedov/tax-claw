namespace TaxClaw.Law.Model;

/// <summary>
/// The law editions pinned to a project — one <see cref="LawVersion"/> per act, chosen for the
/// project's tax year. Deterministic: grounding gates and retrieval resolve against these exact
/// editions, so a rule can never silently apply across editions.
/// </summary>
public sealed class LawVersionSet
{
    private readonly IReadOnlyDictionary<string, LawVersion> _editions;

    public LawVersionSet(IEnumerable<LawVersion> editions) =>
        _editions = editions.ToDictionary(e => e.ActNumber);

    /// <summary>The pinned edition for an act, or null if the act is not part of this set.</summary>
    public LawVersion? EditionFor(string actNumber) => _editions.GetValueOrDefault(actNumber);

    /// <summary>
    /// D1 default policy: the edition governing tax year <paramref name="taxYear"/> is the latest
    /// edition effective on or before 31 Dec of that year. Returns null if none qualifies.
    /// </summary>
    public static LawVersion? SelectForTaxYear(IEnumerable<LawVersion> editions, int taxYear)
    {
        var yearEnd = new DateOnly(taxYear, 12, 31);
        return editions
            .Where(e => e.EffectiveOn <= yearEnd)
            .OrderByDescending(e => e.EffectiveOn)
            .FirstOrDefault();
    }
}
