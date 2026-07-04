using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TaxClaw.Law.Corpus;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Retrieval;

/// <summary>
/// Keyword retriever over the corpus's SQLite FTS5 index (`unicode61 remove_diacritics 2`, bm25).
/// Edition-scoped. Measured (tools/law-retrieval-eval): strong on discovery queries when the query
/// is in Czech legal vocabulary; definitional sections are better served by addressed lookup.
/// </summary>
public sealed partial class FtsLawRetriever(SqliteLawCorpus corpus) : ILawRetriever
{
    public IReadOnlyList<LawSearchResult> Search(string queryCz, LawVersion version, int k = 5)
    {
        string? match = ToMatch(queryCz);
        if (match is null)
        {
            return [];
        }

        var hits = new List<(string Section, double Score)>();
        using (SqliteCommand cmd = corpus.Connection.CreateCommand())
        {
            cmd.CommandText =
                """
                SELECT section, bm25(sections_fts) AS score
                FROM sections_fts
                WHERE sections_fts MATCH $match AND version_eli = $version
                ORDER BY score
                LIMIT $k;
                """;
            cmd.Parameters.AddWithValue("$match", match);
            cmd.Parameters.AddWithValue("$version", version.Eli);
            cmd.Parameters.AddWithValue("$k", k);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                hits.Add((reader.GetString(0), reader.GetDouble(1)));
            }
        }

        var results = new List<LawSearchResult>(hits.Count);
        foreach ((string section, double score) in hits)
        {
            LawSection? resolved = corpus.Resolve(section, version);
            if (resolved is not null)
            {
                // bm25 returns lower = more relevant; expose higher = more relevant.
                results.Add(new LawSearchResult(resolved, -score));
            }
        }
        return results;
    }

    /// <summary>Builds an FTS5 MATCH: unicode word tokens (len ≥ 2), quoted, OR-joined.</summary>
    private static string? ToMatch(string query)
    {
        var tokens = Word().Matches(query)
            .Select(m => m.Value)
            .Where(t => t.Length >= 2)
            .Select(t => $"\"{t}\"")
            .ToList();
        return tokens.Count == 0 ? null : string.Join(" OR ", tokens);
    }

    [GeneratedRegex(@"\w+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}
