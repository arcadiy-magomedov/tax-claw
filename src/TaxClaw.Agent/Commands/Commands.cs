using TaxClaw.Core.Model;

namespace TaxClaw.Agent.Commands;

/// <summary>A parsed TUI input line.</summary>
public abstract record TuiCommand;

public sealed record NewProjectCommand(TaxYear Year) : TuiCommand;

/// <summary>Load the in-force legislation for a tax year into the active law session.</summary>
public sealed record LoadLawCommand(TaxYear Year) : TuiCommand;

/// <summary>Show the current model / list models (ModelId null) or switch to a model.</summary>
public sealed record ModelCommand(string? ModelId) : TuiCommand;

public sealed record ChatCommand(string Message) : TuiCommand;

public sealed record QuitCommand : TuiCommand;

public sealed record UnknownCommand(string Reason) : TuiCommand;
