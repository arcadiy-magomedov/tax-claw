using Spectre.Console;
using TaxClaw.Agent;
using TaxClaw.Agent.Commands;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;
using Profile = TaxClaw.Core.Model.Profile;

namespace TaxClaw.Tui;

/// <summary>Drives the interactive loop: read a line, route it, act, print.</summary>
public sealed class AppHost(TaxClawAgent agent, IProfileStore profiles, IProjectStore projects)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        AnsiConsole.Write(new FigletText("tax-claw").Color(Color.Teal));
        AnsiConsole.MarkupLine("[grey]Type [/][teal]/new 2027[/][grey] to start a project, or just chat. [/][teal]/quit[/][grey] to exit.[/]");

        while (!ct.IsCancellationRequested)
        {
            string line = AnsiConsole.Prompt(new TextPrompt<string>("[teal]›[/]").AllowEmpty());
            TuiCommand command = CommandRouter.Parse(line);

            switch (command)
            {
                case QuitCommand:
                    return;

                case NewProjectCommand np:
                    await CreateProjectAsync(np.Year, ct);
                    break;

                case ChatCommand chat when chat.Message.Length > 0:
                    await AnsiConsole.Status().StartAsync("thinking…", async _ =>
                    {
                        string reply = await agent.SendAsync(chat.Message, ct);
                        AnsiConsole.MarkupLine($"[white]{Markup.Escape(reply)}[/]");
                    });
                    break;

                case UnknownCommand unknown:
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(unknown.Reason)}[/]");
                    break;
            }
        }
    }

    private async Task CreateProjectAsync(TaxYear year, CancellationToken ct)
    {
        Profile profile = await profiles.LoadAsync(ct) ?? PromptForProfile();

        // Re-confirm the profile for every new project, then snapshot it.
        if (!AnsiConsole.Confirm($"Use profile for [teal]{Markup.Escape(profile.FullName)}[/]?"))
        {
            profile = PromptForProfile();
        }

        await profiles.SaveAsync(profile, ct);

        try
        {
            var project = await projects.CreateAsync(year, profile, ct);
            AnsiConsole.MarkupLine($"[green]Created project[/] [teal]{project.Id}[/] [grey](status: {project.Status})[/].");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(ex.Message)}[/]");
        }
    }

    private static Profile PromptForProfile() => new()
    {
        FullName = AnsiConsole.Prompt(new TextPrompt<string>("Full name:")),
        RodneCislo = NullIfBlank(AnsiConsole.Prompt(new TextPrompt<string>("Rodné číslo (optional):").AllowEmpty())),
        Employer = NullIfBlank(AnsiConsole.Prompt(new TextPrompt<string>("Employer (optional):").AllowEmpty()))
    };

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
