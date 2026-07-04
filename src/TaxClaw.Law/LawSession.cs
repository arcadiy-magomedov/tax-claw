using TaxClaw.Law.Corpus;
using TaxClaw.Law.Grounding;
using TaxClaw.Law.Ingest;
using TaxClaw.Law.Model;
using TaxClaw.Law.Retrieval;

namespace TaxClaw.Law;

/// <summary>
/// The law-grounding context for the active project: the pinned edition and a corpus loaded for it,
/// with tools and the claims checker bound once to the session. Starts empty ("no law loaded");
/// <see cref="LoadAsync"/> populates it when a project's edition is opened. Implements the corpus and
/// retriever seams by delegating to the currently-loaded edition, so the bound tools never go stale.
/// </summary>
public sealed class LawSession : ILawCorpus, ILawRetriever, IDisposable
{
    private SqliteLawCorpus? _corpus;
    private FtsLawRetriever? _retriever;
    private readonly ClaimsGroundingChecker _checker;

    public LawSession()
    {
        Tools = new LawTools(this, this, () => CurrentEdition);
        _checker = new ClaimsGroundingChecker(new LawGroundingGate(this));
    }

    public LawVersion? CurrentEdition { get; private set; }
    public int SectionCount { get; private set; }
    public LawTools Tools { get; }

    /// <summary>Loads a specific edition from the given source, replacing any previously loaded one.</summary>
    public async Task LoadAsync(ILawSource source, LawVersion edition, CancellationToken ct = default)
    {
        IReadOnlyList<LawSection> sections = await source.LoadAsync(edition, ct);

        var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync(sections, ct);

        _corpus?.Dispose();
        _corpus = corpus;
        _retriever = new FtsLawRetriever(corpus);
        CurrentEdition = edition;
        SectionCount = sections.Count;
    }

    /// <summary>Annotates an agent answer with a grounding check against the active edition (no-op if none).</summary>
    public string Annotate(string answer) =>
        CurrentEdition is { } edition ? _checker.Annotate(answer, edition) : answer;

    // ILawCorpus / ILawRetriever — delegate to the loaded edition, or behave empty when none.
    public Task IngestAsync(IEnumerable<LawSection> sections, CancellationToken ct = default) =>
        _corpus?.IngestAsync(sections, ct) ?? Task.CompletedTask;

    public LawSection? Resolve(string section, LawVersion version) => _corpus?.Resolve(section, version);

    public IReadOnlyList<LawSearchResult> Search(string queryCz, LawVersion version, int k = 5) =>
        _retriever?.Search(queryCz, version, k) ?? [];

    public void Dispose() => _corpus?.Dispose();
}
