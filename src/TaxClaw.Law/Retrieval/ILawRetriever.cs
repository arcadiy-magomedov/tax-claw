using TaxClaw.Law.Model;

namespace TaxClaw.Law.Retrieval;

/// <summary>A search hit: the matched section and its relevance score (higher = more relevant).</summary>
public sealed record LawSearchResult(LawSection Match, double Score);

/// <summary>
/// Discovery-path retrieval over the law corpus. The query is expected in <b>Czech legal terms</b>
/// (English keyword search measured at 0 recall — see tools/law-retrieval-eval); the agent expands
/// /translates before calling. Results are edition-scoped. This seam allows a semantic/vector
/// retriever to be added later without changing callers.
/// </summary>
public interface ILawRetriever
{
    IReadOnlyList<LawSearchResult> Search(string queryCz, LawVersion version, int k = 5);
}
