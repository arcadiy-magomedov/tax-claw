using Microsoft.Data.Sqlite;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Corpus;

/// <summary>
/// SQLite-backed <see cref="ILawCorpus"/>. Sections are keyed by (section, edition) so lookups are
/// exact and edition-scoped. Defaults to an in-memory database (the corpus is small and re-imported
/// from the cached e-Sbírka export); pass a file connection string to persist.
/// </summary>
public sealed class SqliteLawCorpus : ILawCorpus, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteLawCorpus(string connectionString = "Data Source=:memory:")
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        Execute(
            """
            CREATE TABLE IF NOT EXISTS sections (
              section     TEXT NOT NULL,
              version_eli TEXT NOT NULL,
              citation    TEXT NOT NULL,
              text        TEXT NOT NULL,
              source_eli  TEXT NOT NULL,
              hash        TEXT NOT NULL,
              PRIMARY KEY (section, version_eli)
            );
            """);
    }

    /// <summary>The live connection, shared with the FTS retriever built over the same corpus.</summary>
    internal SqliteConnection Connection => _connection;

    public async Task IngestAsync(IEnumerable<LawSection> sections, CancellationToken ct = default)
    {
        await using SqliteTransaction tx = (SqliteTransaction)await _connection.BeginTransactionAsync(ct);
        foreach (LawSection s in sections)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT OR REPLACE INTO sections (section, version_eli, citation, text, source_eli, hash)
                VALUES ($section, $version, $citation, $text, $source, $hash);
                """;
            cmd.Parameters.AddWithValue("$section", s.Section);
            cmd.Parameters.AddWithValue("$version", s.Version.Eli);
            cmd.Parameters.AddWithValue("$citation", s.CitationLabel);
            cmd.Parameters.AddWithValue("$text", s.Text);
            cmd.Parameters.AddWithValue("$source", s.SourceEli);
            cmd.Parameters.AddWithValue("$hash", s.Hash);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public LawSection? Resolve(string section, LawVersion version)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT citation, text, source_eli FROM sections WHERE section = $section AND version_eli = $version;";
        cmd.Parameters.AddWithValue("$section", section);
        cmd.Parameters.AddWithValue("$version", version.Eli);

        using SqliteDataReader reader = cmd.ExecuteReader();
        return reader.Read()
            ? new LawSection(section, reader.GetString(0), reader.GetString(1), version, reader.GetString(2))
            : null;
    }

    private void Execute(string sql)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
