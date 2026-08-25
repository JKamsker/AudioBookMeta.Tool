using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigUnsetSettings : GlobalSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("Dot-separated key or provider to remove.")]
    public required string Key { get; init; }

    [CommandOption("--dry-run")]
    [Description("Preview the removal without changing the file.")]
    public bool DryRun { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Confirm removal without prompting.")]
    public bool Yes { get; init; }

    public override ValidationResult Validate()
        => DryRun || Yes
            ? ValidationResult.Success()
            : ValidationResult.Error("Removing a configuration value requires --yes; use --dry-run to preview it.");
}

public sealed class ConfigUnsetCommand(ConfigDocumentStore store, AppConsole console) : Command<ConfigUnsetSettings>
{
    protected override int Execute(CommandContext context, ConfigUnsetSettings settings, CancellationToken cancellationToken)
    {
        var change = store.Unset(settings.ConfigPath, settings.Key, settings.DryRun);
        if (settings.Json)
            console.Json(new
            {
                schema_version = 1,
                path = change.Path,
                key = change.Key,
                removed = !settings.DryRun,
                dry_run = settings.DryRun
            });
        else if (!settings.Quiet)
            console.Out.WriteLine(settings.DryRun
                ? $"Would remove {change.Key} from {change.Path}"
                : $"Removed {change.Key} from {change.Path}");
        return ExitCodes.Success;
    }
}
