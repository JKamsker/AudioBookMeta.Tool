using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Providers;

public sealed class ProvidersListSettings : GlobalSettings;

public sealed class ProvidersListCommand(ConfigLoader loader, ProviderFactory factory, AppConsole console) : Command<ProvidersListSettings>
{
    protected override int Execute(CommandContext context, ProvidersListSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var rows = config.Providers.Values.OrderByDescending(provider => provider.Priority).ThenBy(provider => provider.Id).Select(provider => new
        {
            id = provider.Id,
            adapter_type = provider.Type,
            enabled = provider.Enabled,
            groups = Groups(config, provider),
            base_host = provider.BaseUrl.Host,
            capabilities = CompactCapabilities(factory.Create(provider).Capabilities)
        }).ToList();
        if (settings.Json)
            console.Json(new { schema_version = 1, providers = rows });
        else if (settings.Quiet)
            foreach (var row in rows) console.Out.WriteLine(row.id);
        else
        {
            var table = new Table().Border(TableBorder.Simple).AddColumn("ID").AddColumn("Type").AddColumn("Enabled").AddColumn("Groups").AddColumn("Host").AddColumn("Caps");
            foreach (var row in rows)
                table.AddRow(AppConsole.Safe(row.id), row.adapter_type, row.enabled ? "yes" : "no", AppConsole.Safe(string.Join(',', row.groups)), AppConsole.Safe(row.base_host), AppConsole.Safe(string.Join(',', row.capabilities)));
            console.Out.Write(table);
        }
        return ExitCodes.Success;
    }

    private static List<string> Groups(AudiobookMetaConfig config, ProviderConfig provider)
        => provider.Groups.Concat(config.Groups.Where(group => group.Value.Contains(provider.Id, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    private static List<string> CompactCapabilities(AudiobookMeta.Tool.Model.ProviderCapabilities capabilities)
    {
        var labels = new Dictionary<string, string>
        {
            ["search"] = "search",
            ["quick_search"] = "quick",
            ["get_by_id"] = "get",
            ["asin_filter"] = "ASIN",
            ["isbn_filter"] = "ISBN",
            ["sku_lookup"] = "SKU",
            ["duration_metadata"] = "duration",
            ["shop_links"] = "shops",
            ["health"] = "health"
        };
        return labels.Where(item => capabilities[item.Key] == AudiobookMeta.Tool.Model.CapabilityState.Supported).Select(item => item.Value).ToList();
    }
}
