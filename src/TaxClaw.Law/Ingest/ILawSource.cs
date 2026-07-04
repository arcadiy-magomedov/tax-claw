using TaxClaw.Law.Model;

namespace TaxClaw.Law.Ingest;

/// <summary>Loads all sections (§) of a specific act edition.</summary>
public interface ILawSource
{
    Task<IReadOnlyList<LawSection>> LoadAsync(LawVersion version, CancellationToken ct = default);
}
