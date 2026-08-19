using System.ComponentModel;
using BookMeta.Common;
using BookMeta.Configuration;
using BookMeta.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BookMeta.Commands.Providers;

public sealed class ProvidersShowSettings : GlobalSettings
{
    [CommandArgument(0, "<PROVIDER>")]
    [Description("Configured provider instance ID.")]
    public required string Provider { get; init; }
}

public sealed class ProvidersShowCommand(ConfigLoader loader, ProviderFactory factory, AppConsole console) : Command<ProvidersShowSettings>
{
    protected override int Execute(CommandContext context, ProvidersShowSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        if (!config.Providers.TryGetValue(settings.Provider, out var provider))
            throw new BookMetaException($"Unknown provider '{settings.Provider}'.", ExitCodes.Configuration, "Run 'bookmeta providers list'.");
        var capabilities = factory.Create(provider).Capabilities;
        var output = new
        {
            schema_version = 1,
            provider = new
            {
                id = provider.Id, adapter_type = provider.Type, base_url = provider.BaseUrl.ToString(), provider.Enabled, provider.Region,
                provider.Priority, provider.Groups, timeout = provider.Timeout?.ToString(), provider.AllowInsecureHttp, provider.AppendSearchPath,
                auth = provider.Auth is null ? null : $"{provider.Auth.Split(':', 2)[0]}:<redacted>",
                headers = provider.Headers.ToDictionary(pair => pair.Key, pair => pair.Key.Contains("token", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("key", StringComparison.OrdinalIgnoreCase) ? "<redacted>" : pair.Value),
                provider.QueryParams,
                capabilities = capabilities.Values.ToDictionary(pair => pair.Key, pair => CapabilityCatalog.Display(pair.Value)),
                capability_sources = capabilities.Sources,
                source_notes = ProviderNotes.ForType(provider.Type)
            }
        };
        if (settings.Json)
            console.Json(output);
        else
        {
            console.Out.MarkupLine($"[bold]{AppConsole.Safe(provider.Id)}[/]");
            console.Out.MarkupLine($"Type: {AppConsole.Safe(provider.Type)}");
            console.Out.MarkupLine($"Enabled: {(provider.Enabled ? "yes" : "no")}");
            console.Out.MarkupLine($"Base URL: {AppConsole.Safe(provider.BaseUrl.ToString())}");
            console.Out.MarkupLine($"Auth: {AppConsole.Safe(output.provider.auth)}");
            console.Out.MarkupLine($"Notes: {AppConsole.Safe(ProviderNotes.ForType(provider.Type))}");
            console.Out.MarkupLine("Capabilities:");
            foreach (var capability in capabilities.Values.OrderBy(item => item.Key))
                console.Out.MarkupLine($"  {AppConsole.Safe(capability.Key)}: {CapabilityCatalog.Display(capability.Value)} ({AppConsole.Safe(capabilities.Sources.GetValueOrDefault(capability.Key))})");
        }
        return ExitCodes.Success;
    }
}
