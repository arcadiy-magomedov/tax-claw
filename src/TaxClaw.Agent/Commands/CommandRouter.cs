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
}
