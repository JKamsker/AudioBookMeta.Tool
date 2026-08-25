using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Providers;

public sealed class ProvidersCapabilitiesSettings : GlobalSettings
{
    [CommandArgument(0, "[PROVIDER]")]
    [Description("Provider IDs; omit to show all configured providers.")]
    public string[] Providers { get; init; } = [];
}

public sealed class ProvidersCapabilitiesCommand(ConfigLoader loader, ProviderFactory factory, AppConsole console) : Command<ProvidersCapabilitiesSettings>
{
    protected override int Execute(CommandContext context, ProvidersCapabilitiesSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var selected = settings.Providers.Length == 0 ? config.Providers.Values.ToList() : settings.Providers.Select(id => config.Providers.TryGetValue(id, out var provider) ? provider : throw new AudiobookMetaException($"Unknown provider '{id}'.", ExitCodes.Configuration)).ToList();
        var rows = selected.Select(provider =>
        {
            var capabilities = factory.Create(provider).Capabilities;
            return new { provider = provider.Id, capabilities = capabilities.Values.ToDictionary(pair => pair.Key, pair => new { value = CapabilityCatalog.Display(pair.Value), source = capabilities.Sources.GetValueOrDefault(pair.Key, "documented") }) };
        }).ToList();
        if (settings.Json)
            console.Json(new { schema_version = 1, providers = rows });
        else
        {
            var table = new Table().Border(TableBorder.Simple).AddColumn("Provider").AddColumn("Capability").AddColumn("Value").AddColumn("Source");
            foreach (var row in rows)
                foreach (var capability in row.capabilities.OrderBy(item => item.Key))
                    table.AddRow(AppConsole.Safe(row.provider), AppConsole.Safe(capability.Key), capability.Value.value, capability.Value.source);
            console.Out.Write(table);
        }
        return ExitCodes.Success;
    }
}
