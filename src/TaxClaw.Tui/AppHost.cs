using Microsoft.Agents.AI;
using Spectre.Console;
using TaxClaw.Agent;
using TaxClaw.Agent.Commands;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;
using TaxClaw.Documents;
using TaxClaw.Documents.Model;
using TaxClaw.Export;
using TaxClaw.Export.Xml;
using TaxClaw.Law;
using TaxClaw.Law.Ingest;
using TaxClaw.Law.Model;
using TaxClaw.Llm;
using Profile = TaxClaw.Core.Model.Profile;

namespace TaxClaw.Tui;

/// <summary>Drives the interactive loop: read a line, route it, act, print.</summary>
public sealed class AppHost(
    TaxClawAgent agent,
    IProfileStore profiles,
    IProjectStore projects,
    LlmOptions llmOptions,
    Func<AIAgent> buildAgent,
    LawSession lawSession,
    ILawSource lawSource,
    DocumentPipeline documentPipeline,
    SessionState sessionState,
    IModelCatalog? modelCatalog = null,
    Func<CancellationToken, Task>? persistPreferences = null,
    string? epoXsdPath = null)
{
    private TaxReturn? _currentReturn;

    public async Task RunAsync(CancellationToken ct = default)
    {
        AnsiConsole.Write(new FigletText("tax-claw").Color(Color.Teal));
        AnsiConsole.MarkupLine("[grey]Type [/][teal]/new 2027[/][grey] to start a project, [/][teal]/law 2027[/][grey] to load legislation, [/][teal]/doc <path>[/][grey] to add a document, [/][teal]/export <fmt> <path>[/][grey] to export, [/][teal]/model[/][grey] to change model, or just chat. [/][teal]/quit[/][grey] to exit.[/]");

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

                case LoadLawCommand law:
                    await LoadLawAsync(law.Year, ct);
                    break;

                case ProcessDocumentCommand pd:
                    await ProcessDocumentAsync(pd.Path, ct);
                    break;

                case ExportCommand export:
                    await ExportAsync(export.Format, export.Path, ct);
                    break;

                case ModelCommand model:
                    await HandleModelAsync(model.ModelId, ct);
                    break;

                case ChatCommand chat when chat.Message.Length > 0:
                    await AnsiConsole.Status().StartAsync("thinking…", async _ =>
                    {
                        // Remembered context is injected MAF-natively by the agent (see BuildAgent),
                        // so the loop just forwards the user's message.
                        string reply = lawSession.Annotate(await agent.SendAsync(chat.Message, ct: ct));
                        AnsiConsole.MarkupLine($"[white]{Markup.Escape(reply)}[/]");
                    });
                    break;

                case UnknownCommand unknown:
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(unknown.Reason)}[/]");
                    break;
            }
        }
    }

    /// <summary>
    /// Loads the Income Tax Act (586/1992) edition governing the given tax year (D1 default: latest
    /// edition effective by 31 Dec) from e-Sbírka into the law session, so the agent's tools and
    /// grounding checks work for that year.
    /// </summary>
    private async Task LoadLawAsync(TaxYear year, CancellationToken ct)
    {
        try
        {
            await AnsiConsole.Status().StartAsync($"loading law for {year}…",
                async _ => await lawSession.OpenForYearAsync(lawSource, "586/1992", year.Year, ct));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not load law: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        string eli = lawSession.CurrentEdition?.Eli ?? "(none)";
        AnsiConsole.MarkupLine(lawSession.SectionCount == 0
            ? $"[yellow]No sections found for {Markup.Escape(eli)}.[/]"
            : $"[green]Loaded[/] [teal]{Markup.Escape(eli)}[/] [grey]({lawSession.SectionCount} sections, in force for tax year {year}).[/]");
    }

    private async Task HandleModelAsync(string? modelId, CancellationToken ct)
    {
        // Explicit switch: /model <id>
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            await SwitchModelAsync(modelId.Trim(), ct);
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

        // Thinking effort: only offered for models that support it.
        string? effort = PromptForEffort(selected);

        await ApplyModelChangeAsync(selected.Id, effort, ct);
    }

    /// <summary>
    /// Prompts for a reasoning/thinking effort when the model supports it. Returns the chosen effort,
    /// or null to use the model's default. Returns null immediately for non-reasoning models.
    /// </summary>
    private static string? PromptForEffort(ModelOption model)
    {
        if (!model.SupportsReasoningEffort)
        {
            return null;
        }

        const string useDefault = "(model default)";
        var choices = new List<string> { useDefault };
        choices.AddRange(model.SupportedReasoningEfforts);

        string defaultLabel = model.DefaultReasoningEffort is { Length: > 0 } d
            ? $"{useDefault} → {d}"
            : useDefault;

        string pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Thinking effort for [teal]{Markup.Escape(model.Name)}[/] [grey](↑/↓, Enter)[/]:")
                .PageSize(10)
                .UseConverter(c => Markup.Escape(c == useDefault ? defaultLabel : c))
                .AddChoices(choices));

        return pick == useDefault ? null : pick;
    }

    private string FormatChoice(ModelOption m)
    {
        string current = string.Equals(m.Id, llmOptions.Model, StringComparison.OrdinalIgnoreCase)
            ? "  (current)"
            : string.Empty;

        var tags = new List<string>();
        if (m.MaxContextWindowTokens is long ctx)
        {
            tags.Add($"ctx {FormatTokens(ctx)}");
        }
        if (m.SupportsReasoningEffort)
        {
            tags.Add("reasoning");
        }
        string suffix = tags.Count > 0 ? $"  [{string.Join(", ", tags)}]" : string.Empty;

        return $"{m.Name} — {m.Id}{suffix}{current}";
    }

    private static string FormatTokens(long tokens) => tokens switch
    {
        >= 1_000_000 => $"{tokens / 1_000_000.0:0.#}M",
        >= 1_000 => $"{tokens / 1_000.0:0.#}K",
        _ => tokens.ToString()
    };

    private Task SwitchModelAsync(string modelId, CancellationToken ct) => ApplyModelChangeAsync(modelId, null, ct);

    /// <summary>
    /// Applies a model (and optional reasoning effort) change, rebuilding the chat client while
    /// preserving conversation context, then persists the preference. Rolls back both fields if the
    /// rebuild fails.
    /// </summary>
    private async Task ApplyModelChangeAsync(string modelId, string? effort, CancellationToken ct)
    {
        string previousModel = llmOptions.Model;
        string? previousEffort = llmOptions.ReasoningEffort;

        bool sameModel = string.Equals(modelId, previousModel, StringComparison.OrdinalIgnoreCase);
        bool sameEffort = string.Equals(effort, previousEffort, StringComparison.OrdinalIgnoreCase);
        if (sameModel && sameEffort)
        {
            AnsiConsole.MarkupLine($"[grey]Already using [/][teal]{Markup.Escape(modelId)}[/][grey].[/]");
            return;
        }

        llmOptions.Model = modelId;
        llmOptions.ReasoningEffort = effort;
        try
        {
            await agent.UseAgentAsync(buildAgent(), ct);
        }
        catch (Exception ex)
        {
            llmOptions.Model = previousModel;
            llmOptions.ReasoningEffort = previousEffort;
            AnsiConsole.MarkupLine($"[yellow]Could not switch model: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        if (persistPreferences is not null)
        {
            try
            {
                await persistPreferences(ct);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Model switched, but saving the preference failed: {Markup.Escape(ex.Message)}[/]");
            }
        }

        string effortNote = effort is { Length: > 0 } ? $", effort: {Markup.Escape(effort)}" : string.Empty;
        AnsiConsole.MarkupLine(
            $"[green]Switched model to[/] [teal]{Markup.Escape(llmOptions.Model)}[/][grey]{effortNote} (conversation context preserved).[/]");
    }

    private async Task ExportAsync(string format, string path, CancellationToken ct)
    {
        if (_currentReturn is not { } ret)
        {
            AnsiConsole.MarkupLine("[yellow]No active project. Start one first with [/][teal]/new <year>[/][yellow].[/]");
            return;
        }

        string expanded = ExpandPath(path);
        try
        {
            switch (format)
            {
                case "summary":
                    await File.WriteAllTextAsync(expanded, new SummaryExporter().Export(ret), ct);
                    break;
                case "xml":
                    string xml = new XmlExporter().Export(ret);
                    await File.WriteAllTextAsync(expanded, xml, ct);
                    ReportXmlValidation(xml);
                    break;
                case "pdf":
                    await File.WriteAllBytesAsync(expanded, new PdfExporter().Export(ret), ct);
                    break;
                default:
                    AnsiConsole.MarkupLine($"[yellow]Unknown format '{Markup.Escape(format)}'. Use summary, pdf, or xml.[/]");
                    return;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Export failed: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Exported[/] [teal]{Markup.Escape(format)}[/] [grey]→ {Markup.Escape(expanded)}[/].");
    }

    private void ReportXmlValidation(string xml)
    {
        EpoSchema schema = EpoSchema.Resolve(epoXsdPath);
        XsdReport report = XsdValidator.Validate(xml, schema.Text);
        if (report.IsValid)
        {
            AnsiConsole.MarkupLine($"[grey]Validated against {Markup.Escape(schema.Source)}.[/]");
        }
        else
        {
            string detail = string.Join("; ", report.Errors.Take(3));
            AnsiConsole.MarkupLine(
                $"[yellow]Note: XML did not validate against {Markup.Escape(schema.Source)}: {Markup.Escape(detail)}[/]");
        }
    }

    private async Task ProcessDocumentAsync(string path, CancellationToken ct)
    {
        if (_currentReturn is not { } current)
        {
            AnsiConsole.MarkupLine("[yellow]No active project. Start one first with [/][teal]/new <year>[/][yellow].[/]");
            return;
        }

        string expanded = ExpandPath(path);
        if (!File.Exists(expanded))
        {
            AnsiConsole.MarkupLine($"[yellow]File not found: {Markup.Escape(expanded)}[/]");
            return;
        }

        DocumentResult result;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(expanded, ct);
            string name = Path.GetFileName(expanded);
            var doc = SourceDocument.FromBytes(name, bytes);
            result = await AnsiConsole.Status().StartAsync("processing document…",
                async _ => await documentPipeline.ProcessAsync(doc, current, name, ct));
        }
        catch (NotSupportedException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(ex.Message)}[/]");
            return;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not process document: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        AnsiConsole.MarkupLine(
            $"[grey]Classified as[/] [teal]{result.Type}[/] [grey](confidence {result.Confidence:0.0}).[/]");

        if (result.Validation.IsValid)
        {
            _currentReturn = result.Return;
            AnsiConsole.MarkupLine($"[green]Added to the return.[/] [grey]Income items: {_currentReturn.Incomes.Count}.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Missing required fields: {Markup.Escape(string.Join(", ", result.Validation.MissingFields))}. "
                + "Not added — please confirm or fill these.[/]");
        }
    }

    private static string ExpandPath(string path) =>
        path.StartsWith('~')
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/', '\\'))
            : path;

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
            _currentReturn = new TaxReturn(year);
            sessionState.ActiveProjectId = year.Year.ToString();
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
