namespace TaxClaw.Agent.Commands;

/// <summary>
/// Splits a raw <c>/doc</c> argument string into one or more path tokens, handling the quoting/
/// escaping conventions terminals use when a file is dragged in from Finder/Explorer or a path is
/// pasted: backslash-escaped spaces (<c>My\ File.pdf</c>) and single- or double-quoted whole paths
/// (<c>'My File.pdf'</c>, <c>"My File.pdf"</c>). This is deliberately not a full shell-command
/// tokenizer (no globs, variables, or command substitution) — just enough to recover literal paths.
/// </summary>
public static class PathArgumentParser
{
    public static IReadOnlyList<string> Tokenize(string raw)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool hasToken = false;
        int i = 0;

        while (i < raw.Length)
        {
            char c = raw[i];

            if (char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
                i++;
                continue;
            }

            hasToken = true;

            if (c == '\'')
            {
                i++;
                while (i < raw.Length && raw[i] != '\'')
                {
                    current.Append(raw[i]);
                    i++;
                }
                i++; // skip closing quote (if any — an unterminated quote just reads to end)
                continue;
            }

            if (c == '"')
            {
                i++;
                while (i < raw.Length && raw[i] != '"')
                {
                    if (raw[i] == '\\' && i + 1 < raw.Length && (raw[i + 1] == '"' || raw[i + 1] == '\\'))
                    {
                        current.Append(raw[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        current.Append(raw[i]);
                        i++;
                    }
                }
                i++; // skip closing quote
                continue;
            }

            if (c == '\\' && i + 1 < raw.Length)
            {
                current.Append(raw[i + 1]);
                i += 2;
                continue;
            }

            current.Append(c);
            i++;
        }

        if (hasToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
