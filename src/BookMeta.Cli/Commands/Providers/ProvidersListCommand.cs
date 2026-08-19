using BookMeta.Common;
using BookMeta.Configuration;
using BookMeta.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BookMeta.Commands.Providers;

public sealed class ProvidersListSettings : GlobalSettings;

public sealed class ProvidersListCommand(ConfigLoader loader, ProviderFactory factory, AppConsole console) : Command<ProvidersListSettings>
{
    protected override int Execute(CommandContext context, ProvidersListSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var rows = config.Providers.Values.OrderByDescending(provider => provider.Priority).ThenBy(provider => provider.Id).Select(provider => new
        {
            id = provider.Id, adapter_type = provider.Type, enabled = provider.Enabled,
            groups = Groups(config, provider), base_host = provider.BaseUrl.Host,
            capabilities = factory.Create(provider).Capabilities.Values.Where(item => item.Value == BookMeta.Model.CapabilityState.Supported).Select(item => item.Key).Order().ToList()
        }).ToList();
        if (settings.Json)
            console.Json(new { schema_version = 1, providers = rows });
        else if (settings.Quiet)
            foreach (var row in rows) console.Out.WriteLine(row.id);
        else
        {
            var table = new Table().Border(TableBorder.Simple).AddColumn("ID").AddColumn("Type").AddColumn("Enabled").AddColumn("Groups").AddColumn("Host").AddColumn("Capabilities");
            foreach (var row in rows)
                table.AddRow(AppConsole.Safe(row.id), row.adapter_type, row.enabled ? "yes" : "no", AppConsole.Safe(string.Join(',', row.groups)), AppConsole.Safe(row.base_host), AppConsole.Safe(string.Join(',', row.capabilities)));
            console.Out.Write(table);
        }
        return ExitCodes.Success;
    }

    private static List<string> Groups(BookMetaConfig config, ProviderConfig provider)
        => provider.Groups.Concat(config.Groups.Where(group => group.Value.Contains(provider.Id, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
}
