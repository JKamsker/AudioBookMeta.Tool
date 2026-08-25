using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Render;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Author;

public sealed class AuthorBooksSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    [Description("Author name to look up through the provider's native author endpoint.")]
    public string Name { get; init; } = string.Empty;

    [CommandOption("-p|--provider <ID>")]
    [Description("Libex provider instance. Required when multiple Libex instances are enabled.")]
    public string? Provider { get; init; }

    [CommandOption("--region <CODE>")]
    [Description("Audible region code; overrides the selected provider's configured region.")]
    public string? Region { get; init; }

    [CommandOption("--timeout <DURATION>")]
    [Description("Request timeout such as 10s or 1m.")]
    public string? Timeout { get; init; }

    [CommandOption("--raw")]
    [Description("Include the unversioned provider payload; requires --json.")]
    public bool Raw { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return ValidationResult.Error("Author name cannot be empty.");
        if (Raw && !Json)
            return ValidationResult.Error("--raw requires --json.");
        if (Timeout is not null && !DurationParser.TryParse(Timeout, out _))
            return ValidationResult.Error("--timeout must be a positive duration such as 10s or 1m.");
        return ValidationResult.Success();
    }
}

public sealed class AuthorBooksCommand(
    ConfigLoader loader,
    ProviderFactory factory,
    AudiobookRenderer renderer,
    AppConsole console) : AsyncCommand<AuthorBooksSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        AuthorBooksSettings settings,
        CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var providerConfig = ResolveProvider(config, settings.Provider);
        if (factory.Create(providerConfig) is not IAuthorBooksProvider provider)
            throw Unsupported(providerConfig.Id);

        var timeout = settings.Timeout is null
            ? providerConfig.Timeout ?? config.Search.ProviderTimeout
            : DurationParser.TryParse(settings.Timeout, out var parsed) ? parsed : config.Search.ProviderTimeout;
        console.Verbose(settings.Verbose, $"config={config.SourcePath}; provider={providerConfig.Id}; timeout={timeout}");

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);
        var author = settings.Name.Trim();
        var region = settings.Region ?? providerConfig.Region;
        var results = await provider.GetByAuthorAsync(author, region, settings.Raw, linked.Token);

        if (settings.Json)
            console.Json(new { schema_version = 1, request = new { author, provider = providerConfig.Id, region }, results });
        else if (settings.Quiet)
            foreach (var result in results)
                console.Out.WriteLine($"{AppConsole.Safe(result.ProviderRecordId)}\t{AppConsole.Safe(result.Title)}");
        else
            renderer.RenderList(results);
        return ExitCodes.Success;
    }

    private static ProviderConfig ResolveProvider(AudiobookMetaConfig config, string? id)
    {
        if (id is not null)
        {
            if (!config.Providers.TryGetValue(id, out var selected))
                throw new AudiobookMetaException($"Unknown provider '{id}'.", ExitCodes.Configuration, "Run 'dotnet audiobookmeta providers list'.");
            if (!selected.Enabled)
                throw new AudiobookMetaException($"Provider '{id}' is disabled.", ExitCodes.Configuration, "Enable it before looking up an author.");
            return selected;
        }

        var candidates = config.Providers.Values.Where(provider => provider.Enabled && provider.Type == "libex").ToList();
        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new AudiobookMetaException("No enabled Libex provider is configured.", ExitCodes.Configuration, "Configure a Libex provider and retry."),
            _ => throw new AudiobookMetaException("Multiple Libex providers are enabled.", ExitCodes.Usage, "Select one with '--provider ID'.")
        };
    }

    private static AudiobookMetaException Unsupported(string provider)
        => new($"Provider '{provider}' does not support native author-book lookup.", ExitCodes.UnsupportedCapability, "Select a Libex provider or use 'dotnet audiobookmeta search --author NAME'.");
}
