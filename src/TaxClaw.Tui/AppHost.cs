using Microsoft.Extensions.AI;
using Spectre.Console;
using TaxClaw.Agent;
using TaxClaw.Agent.Commands;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;
using TaxClaw.Llm;
using Profile = TaxClaw.Core.Model.Profile;

namespace TaxClaw.Tui;

/// <summary>Drives the interactive loop: read a line, route it, act, print.</summary>
public sealed class AppHost(
    TaxClawAgent agent,
    IProfileStore profiles,
    IProjectStore projects,
    LlmOptions llmOptions,
    Func<IChatClient> buildChatClient,
    IModelCatalog? modelCatalog = null)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        AnsiConsole.Write(new FigletText("tax-claw").Color(Color.Teal));
        AnsiConsole.MarkupLine("[grey]Type [/][teal]/new 2027[/][grey] to start a project, [/][teal]/model[/][grey] to change model, or just chat. [/][teal]/quit[/][grey] to exit.[/]");

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

                case ModelCommand model:
                    await HandleModelAsync(model.ModelId, ct);
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

    private async Task HandleModelAsync(string? modelId, CancellationToken ct)
    {
        // Explicit switch: /model <id>
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            SwitchModel(modelId.Trim());
            return;
        }

        // Interactive picker: /model
        AnsiConsole.MarkupLine(
            $"[grey]Current model:[/] [teal]{Markup.Escape(llmOptions.Model)}[/] [grey](provider: {Markup.Escape(llmOptions.Provider)})[/]");

        if (modelCatalog is null)
        {
            AnsiConsole.MarkupLine("[grey]Model selection isn't available for this provider. Use [/][teal]/model <id>[/][grey] to switch.[/]");
            return;
        }

        IReadOnlyList<ModelOption> models;
        try
        {
            models = await AnsiConsole.Status()
                .StartAsync("loading models…", async _ => await modelCatalog.ListAsync(ct));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not list models: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        if (models.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No models available.[/]");
            return;
        }

        const string cancel = "(cancel)";
        var labels = models.ToDictionary(FormatChoice, m => m);

        string choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a model [grey](↑/↓ to move, type to search, Enter to confirm)[/]:")
                .PageSize(15)
                .MoreChoicesText("[grey](move up and down to reveal more models)[/]")
                .EnableSearch()
                .UseConverter(Markup.Escape)
                .AddChoices(labels.Keys)
                .AddChoices(cancel));

        if (choice == cancel || !labels.TryGetValue(choice, out ModelOption selected))
        {
            return;
        }

        SwitchModel(selected.Id);
    }

    private string FormatChoice(ModelOption m)
    {
        string current = string.Equals(m.Id, llmOptions.Model, StringComparison.OrdinalIgnoreCase)
            ? "  (current)"
            : string.Empty;
        return $"{m.Name} — {m.Id}{current}";
    }

    private void SwitchModel(string modelId)
    {
        string previous = llmOptions.Model;
        if (string.Equals(modelId, previous, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[grey]Already using [/][teal]{Markup.Escape(modelId)}[/][grey].[/]");
            return;
        }

        llmOptions.Model = modelId;
        try
        {
            agent.UseClient(buildChatClient());
            AnsiConsole.MarkupLine(
                $"[green]Switched model to[/] [teal]{Markup.Escape(llmOptions.Model)}[/] [grey](conversation context preserved).[/]");
        }
        catch (Exception ex)
        {
            llmOptions.Model = previous;
            AnsiConsole.MarkupLine($"[yellow]Could not switch model: {Markup.Escape(ex.Message)}[/]");
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
