using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Render;
using AudiobookMeta.Tool.Search;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands;

public sealed class GetSettings : GlobalSettings
{
    [CommandArgument(0, "<PROVIDER:ID>")]
    [Description("Provider instance and native record ID.")]
    public required string Reference { get; init; }
    [CommandOption("--raw")]
    public bool Raw { get; init; }
    [CommandOption("--region <CODE>")]
    [Description("Provider region or locale; overrides the selected provider's configured value.")]
    public string? Region { get; init; }

    public override ValidationResult Validate()
    {
        var index = Reference.IndexOf(':');
        if (index <= 0 || index >= Reference.Length - 1)
            return ValidationResult.Error("Reference must use PROVIDER:ID form.");
        return Raw && !Json ? ValidationResult.Error("--raw requires --json.") : ValidationResult.Success();
    }
}

public sealed class GetCommand(ConfigLoader loader, ProviderFactory factory, ResultClusterer clusterer, AudiobookRenderer renderer, AppConsole console)
    : AsyncCommand<GetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, GetSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var split = settings.Reference.IndexOf(':');
        var providerId = settings.Reference[..split];
        var recordId = settings.Reference[(split + 1)..];
        if (!config.Providers.TryGetValue(providerId, out var provider))
            throw new AudiobookMetaException($"Unknown provider '{providerId}'.", ExitCodes.Configuration, "Run 'dotnet audiobookmeta providers list'.");
        if (!provider.Enabled)
            throw new AudiobookMetaException($"Provider '{providerId}' is disabled.", ExitCodes.Configuration, "Enable it before using get.");

        var adapter = factory.Create(provider);
        if (settings.Region is not null && adapter.Capabilities["region_filter"] != CapabilityState.Supported)
            throw new AudiobookMetaException($"Provider '{providerId}' does not support region selection.", ExitCodes.UnsupportedCapability, "Remove --region or select a provider that supports regions or locales.");

        using var timeout = new CancellationTokenSource(provider.Timeout ?? config.Search.ProviderTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        var result = await adapter.GetAsync(recordId, settings.Region ?? provider.Region, settings.Raw, linked.Token);
        result.Score = 100;
        result.Confidence = "exact";
        clusterer.AssignClusters([result], false);
        if (settings.Json)
            console.Json(new { schema_version = 1, result });
        else if (settings.Quiet)
            console.Out.WriteLine(result.ProviderRecordId ?? settings.Reference);
        else
            renderer.RenderDetails(result);
        return ExitCodes.Success;
    }
}
