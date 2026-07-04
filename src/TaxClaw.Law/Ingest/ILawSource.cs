using TaxClaw.Law.Model;

namespace TaxClaw.Law.Ingest;

/// <summary>Loads act editions and lists which editions exist for an act.</summary>
public interface ILawSource
{
    /// <summary>All sections (§) of a specific act edition.</summary>
    Task<IReadOnlyList<LawSection>> LoadAsync(LawVersion version, CancellationToken ct = default);

    /// <summary>The available editions of an act (by effective date), for edition selection.</summary>
    Task<IReadOnlyList<LawVersion>> ListEditionsAsync(string actNumber, CancellationToken ct = default);
}
