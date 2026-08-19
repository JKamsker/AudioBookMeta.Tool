using System.ComponentModel;
using BookMeta.Common;
using BookMeta.Configuration;
using BookMeta.Providers;
using BookMeta.Search;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BookMeta.Commands;

public sealed class GetSettings : GlobalSettings
{
    [CommandArgument(0, "<PROVIDER:ID>")]
    [Description("Provider instance and native record ID.")]
    public required string Reference { get; init; }
    [CommandOption("--raw")]
    public bool Raw { get; init; }

    public override ValidationResult Validate()
    {
        var index = Reference.IndexOf(':');
        if (index <= 0 || index >= Reference.Length - 1)
            return ValidationResult.Error("Reference must use PROVIDER:ID form.");
        return Raw && !Json ? ValidationResult.Error("--raw requires --json.") : ValidationResult.Success();
    }
}

public sealed class GetCommand(ConfigLoader loader, ProviderFactory factory, ResultClusterer clusterer, AppConsole console)
    : AsyncCommand<GetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, GetSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var split = settings.Reference.IndexOf(':');
        var providerId = settings.Reference[..split];
        var recordId = settings.Reference[(split + 1)..];
        if (!config.Providers.TryGetValue(providerId, out var provider))
            throw new BookMetaException($"Unknown provider '{providerId}'.", ExitCodes.Configuration, "Run 'bookmeta providers list'.");
        if (!provider.Enabled)
            throw new BookMetaException($"Provider '{providerId}' is disabled.", ExitCodes.Configuration, "Enable it before using get.");

        using var timeout = new CancellationTokenSource(provider.Timeout ?? config.Search.ProviderTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        var result = await factory.Create(provider).GetAsync(recordId, settings.Raw, linked.Token);
        result.Score = 100;
        result.Confidence = "exact";
        clusterer.AssignClusters([result], false);
        if (settings.Json)
            console.Json(new { schema_version = 1, result });
        else if (settings.Quiet)
            console.Out.WriteLine(result.ProviderRecordId ?? settings.Reference);
        else
        {
            console.Out.MarkupLine($"[bold]{AppConsole.Safe(result.Title)}[/]");
            console.Out.MarkupLine($"Author: {AppConsole.Safe(string.Join(", ", result.Authors))}");
            console.Out.MarkupLine($"Narrator: {AppConsole.Safe(string.Join(", ", result.Narrators))}");
            console.Out.MarkupLine($"Provider ID: {AppConsole.Safe(result.ProviderRecordId)}");
        }
        return ExitCodes.Success;
    }
}
