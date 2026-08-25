using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Render;
using AudiobookMeta.Tool.Search;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands;

public sealed class SearchSettings : GlobalSettings
{
    [CommandArgument(0, "[QUERY]")]
    [Description("Incomplete or complete free-text query.")]
    public string? Query { get; init; }
    [CommandOption("-p|--provider <ID>")]
    [Description("Restrict to a provider ID or @group. Repeatable.")]
    public string[] Providers { get; init; } = [];
    [CommandOption("--exclude <ID>")]
    public string[] Excludes { get; init; } = [];
    [CommandOption("--group <NAME>")]
    public string[] Groups { get; init; } = [];
    [CommandOption("--title <TEXT>")]
    public string? Title { get; init; }
    [CommandOption("--author <TEXT>")]
    public string? Author { get; init; }
    [CommandOption("--narrator <TEXT>")]
    public string? Narrator { get; init; }
    [CommandOption("--series <TEXT>")]
    public string? Series { get; init; }
    [CommandOption("--isbn <VALUE>")]
    public string? Isbn { get; init; }
    [CommandOption("--asin <VALUE>")]
    public string? Asin { get; init; }
    [CommandOption("--language <CODE>")]
    public string? Language { get; init; }
    [CommandOption("--region <CODE>")]
    public string? Region { get; init; }
    [CommandOption("--page <NUMBER>")]
    [Description("Native zero-based provider page, from 0 to 9; requires native pagination support.")]
    public int? Page { get; init; }
    [CommandOption("--limit <N>")]
    public int? Limit { get; init; }
    [CommandOption("--limit-per-provider <N>")]
    public int? LimitPerProvider { get; init; }
    [CommandOption("--timeout <DURATION>")]
    public string? Timeout { get; init; }
    [CommandOption("--deadline <DURATION>")]
    public string? Deadline { get; init; }
    [CommandOption("--exact")]
    public bool Exact { get; init; }
    [CommandOption("--fresh")]
    public bool Fresh { get; init; }
    [CommandOption("--no-dedupe")]
    public bool NoDedupe { get; init; }
    [CommandOption("--editions")]
    public bool Editions { get; init; }
    [CommandOption("--raw")]
    public bool Raw { get; init; }
    [CommandOption("--explain")]
    public bool Explain { get; init; }
    [CommandOption("--strict")]
    public bool Strict { get; init; }
    [CommandOption("--jsonl")]
    public bool JsonLines { get; init; }

    public override ValidationResult Validate()
    {
        if (!new[] { Query, Title, Author, Narrator, Series, Isbn, Asin }.Any(value => !string.IsNullOrWhiteSpace(value)))
            return ValidationResult.Error("Supply QUERY or at least one of --title, --author, --narrator, --series, --isbn, or --asin.");
        if (Json && JsonLines)
            return ValidationResult.Error("--json and --jsonl are mutually exclusive.");
        if (Raw && !Json && !JsonLines)
            return ValidationResult.Error("--raw requires --json or --jsonl.");
        if (Limit is <= 0 || LimitPerProvider is <= 0)
            return ValidationResult.Error("Limits must be positive integers.");
        if (Page is < SearchLimits.MinimumNativePage or > SearchLimits.MaximumNativePage)
            return ValidationResult.Error($"--page must be between {SearchLimits.MinimumNativePage} and {SearchLimits.MaximumNativePage}.");
        if (Timeout is not null && !DurationParser.TryParse(Timeout, out _))
            return ValidationResult.Error("--timeout must be a positive duration such as 500ms, 4s, or 1m.");
        if (Deadline is not null && !DurationParser.TryParse(Deadline, out _))
            return ValidationResult.Error("--deadline must be a positive duration such as 4s or 1m.");
        return ValidationResult.Success();
    }
}

public sealed class SearchCommand(ConfigLoader loader, SearchEngine engine, SearchRenderer renderer, AppConsole console)
    : AsyncCommand<SearchSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var providerTimeout = Parse(settings.Timeout);
        var deadline = Parse(settings.Deadline);
        console.Verbose(settings.Verbose,
            $"config={config.SourcePath}; timeout={providerTimeout ?? config.Search.ProviderTimeout}; deadline={deadline ?? config.Search.Deadline}; limit={settings.Limit ?? config.Search.Limit}");
        var request = new SearchRequest
        {
            Query = settings.Query,
            Title = settings.Title,
            Author = settings.Author,
            Narrator = settings.Narrator,
            Series = settings.Series,
            Isbn = settings.Isbn,
            Asin = settings.Asin,
            Language = settings.Language,
            Region = settings.Region,
            Page = settings.Page,
            LimitPerProvider = settings.LimitPerProvider ?? config.Search.LimitPerProvider,
            Exact = settings.Exact,
            Editions = settings.Editions
        };
        var execution = await engine.ExecuteAsync(config, request, new SearchOptions
        {
            Includes = settings.Providers,
            Groups = settings.Groups,
            Excludes = settings.Excludes,
            Limit = settings.Limit,
            ProviderTimeout = providerTimeout,
            Deadline = deadline,
            Fresh = settings.Fresh,
            NoDedupe = settings.NoDedupe,
            Strict = settings.Strict,
            IncludeRaw = settings.Raw
        }, cancellationToken);
        console.Verbose(settings.Verbose, $"providers={string.Join(',', execution.Response.ProviderStatus.Select(status => status.Provider))}");
        if (settings.Json)
            console.Json(execution.Response);
        else if (settings.JsonLines)
            renderer.RenderJsonLines(execution.Response);
        else
            renderer.RenderHuman(execution.Response, settings.Editions, settings.NoDedupe, settings.Quiet, settings.Explain);
        return execution.ExitCode;
    }

    private static TimeSpan? Parse(string? value) => value is null ? null : DurationParser.TryParse(value, out var result) ? result : null;
}
