using Microsoft.Data.Sqlite;

namespace TaxClaw.Law.Tests;

// Smoke test: verifies Microsoft.Data.Sqlite works with the pinned (patched) SQLitePCLRaw bundle
// and that FTS5 with the Czech-friendly tokenizer is available. Deleted once the retriever lands.
public class SqliteFts5SmokeTests
{
    [Fact]
    public void Fts5_with_unicode61_diacritics_is_available_and_matches()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using (var create = conn.CreateCommand())
        {
            create.CommandText =
                "CREATE VIRTUAL TABLE d USING fts5(body, tokenize='unicode61 remove_diacritics 2');";
            create.ExecuteNonQuery();
        }
        using (var ins = conn.CreateCommand())
        {
            ins.CommandText = "INSERT INTO d(body) VALUES ('příjmy ze závislé činnosti');";
            ins.ExecuteNonQuery();
        }

        using var q = conn.CreateCommand();
        // query without diacritics must still match (remove_diacritics folding)
        q.CommandText = "SELECT count(*) FROM d WHERE d MATCH 'prijmy';";
        var n = (long)q.ExecuteScalar()!;

        Assert.Equal(1, n);
    }
}
