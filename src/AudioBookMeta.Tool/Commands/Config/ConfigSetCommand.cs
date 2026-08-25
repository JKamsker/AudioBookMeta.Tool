using System.ComponentModel;
using System.Globalization;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using Tomlyn.Model;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigSetSettings : GlobalSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("Dot-separated setting, provider field, group, header, query parameter, or capability.")]
    public required string Key { get; init; }

    [CommandArgument(1, "<VALUE>")]
    [Description("New value. Lists use comma-separated items; strings do not need TOML quotes.")]
    public required string Value { get; init; }
}

public sealed class ConfigSetCommand(ConfigDocumentStore store, AppConsole console) : Command<ConfigSetSettings>
{
    protected override int Execute(CommandContext context, ConfigSetSettings settings, CancellationToken cancellationToken)
    {
        var change = store.Set(settings.ConfigPath, settings.Key, settings.Value);
        if (settings.Json)
            console.Json(new
            {
                schema_version = 1,
                path = change.Path,
                key = change.Key,
                value = change.Value,
                replaced = change.Replaced
            });
        else if (!settings.Quiet)
            console.Out.WriteLine($"Set {change.Key} = {Display(change.Value)} in {change.Path}");
        return ExitCodes.Success;
    }

    private static string Display(object? value)
        => value is TomlArray array
            ? string.Join(',', array.Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)))
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
