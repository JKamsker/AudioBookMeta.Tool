using System.ComponentModel;
using System.Globalization;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using Tomlyn.Model;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigGetSettings : GlobalSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("Dot-separated key, for example search.limit or providers.libex.region.")]
    public required string Key { get; init; }
}

public sealed class ConfigGetCommand(ConfigDocumentStore store, AppConsole console) : Command<ConfigGetSettings>
{
    protected override int Execute(CommandContext context, ConfigGetSettings settings, CancellationToken cancellationToken)
    {
        var result = store.Get(settings.ConfigPath, settings.Key);
        if (settings.Json)
            console.Json(new { schema_version = 1, path = result.Path, key = result.Key, value = result.Value });
        else
            console.Out.WriteLine(Display(result.Value));
        return ExitCodes.Success;
    }

    private static string Display(object? value)
        => value is TomlArray array
            ? string.Join(',', array.Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)))
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
