using TaxClaw.Core.Model;

namespace TaxClaw.Agent.Commands;

/// <summary>A parsed TUI input line.</summary>
public abstract record TuiCommand;

public sealed record NewProjectCommand(TaxYear Year) : TuiCommand;

public sealed record ChatCommand(string Message) : TuiCommand;

public sealed record QuitCommand : TuiCommand;

public sealed record UnknownCommand(string Reason) : TuiCommand;
