using TaxClaw.Core.Model;

namespace TaxClaw.Agent.Commands;

/// <summary>Pure mapping from a raw input line to a <see cref="TuiCommand"/> (no I/O).</summary>
public static class CommandRouter
{
    public static TuiCommand Parse(string line)
    {
        string trimmed = line.Trim();

        if (!trimmed.StartsWith('/'))
        {
            return new ChatCommand(trimmed);
        }

        string[] parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string verb = parts[0].ToLowerInvariant();

        return verb switch
        {
            "/quit" or "/exit" => new QuitCommand(),
            "/new" => ParseNew(parts),
            "/law" => ParseLaw(parts),
            "/doc" => ParseDoc(parts),
            "/export" => ParseExport(parts),
            "/model" or "/models" => new ModelCommand(parts.Length > 1 ? parts[1].Trim() : null),
            _ => new UnknownCommand($"Unknown command '{verb}'.")
        };
    }

    private static TuiCommand ParseNew(string[] parts)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out int year))
        {
            return new UnknownCommand("Usage: /new <year>, e.g. /new 2027");
        }

        try
        {
            return new NewProjectCommand(TaxYear.Of(year));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new UnknownCommand(ex.Message);
        }
    }

    private static TuiCommand ParseLaw(string[] parts)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out int year))
        {
            return new UnknownCommand("Usage: /law <year>, e.g. /law 2027");
        }

        try
        {
            return new LoadLawCommand(TaxYear.Of(year));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new UnknownCommand(ex.Message);
        }
    }

    private static TuiCommand ParseExport(string[] parts)
    {
        string[] args = parts.Length > 1
            ? parts[1].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
            : [];
        if (args.Length < 2)
        {
            return new UnknownCommand("Usage: /export <summary|pdf|xml> <path>");
        }
        return new ExportCommand(args[0].ToLowerInvariant(), args[1].Trim());
    }

    private static TuiCommand ParseDoc(string[] parts)
    {
        const string usage =
            "Usage: /doc <path> [path2 ...] — a file, folder, or .zip/.tar/.tar.gz archive, "
            + "e.g. /doc ~/statements/dividend.txt (drag-and-drop or paste multiple paths at once)";

        if (parts.Length < 2 || parts[1].Trim().Length == 0)
        {
            return new UnknownCommand(usage);
        }

        IReadOnlyList<string> paths = PathArgumentParser.Tokenize(parts[1]);
        return paths.Count > 0 ? new ProcessDocumentCommand(paths) : new UnknownCommand(usage);
    }
}
