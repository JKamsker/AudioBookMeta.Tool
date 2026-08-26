using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigMigrateSettings : GlobalSettings
{
    [CommandOption("--dry-run")]
    [Description("Show exact changes without modifying the configuration.")]
    public bool DryRun { get; init; }

    [CommandOption("--apply <KEY>")]
    [Description("Adopt the current default for one reported key. Repeatable.")]
    public string[] Apply { get; init; } = [];

    [CommandOption("--all")]
    [Description("Adopt every value recognized as an old generated default; custom values remain unchanged.")]
    public bool All { get; init; }

    public override ValidationResult Validate()
        => DryRun || All || Apply.Length > 0
            ? ValidationResult.Success()
            : ValidationResult.Error("Migration requires --dry-run, --all, or at least one --apply KEY selection.");
}

public sealed class ConfigMigrateCommand(ConfigMigrationService migrations, AppConsole console) : Command<ConfigMigrateSettings>
{
    protected override int Execute(CommandContext context, ConfigMigrateSettings settings, CancellationToken cancellationToken)
    {
        var result = migrations.Migrate(settings.ConfigPath, settings.Apply, settings.All, settings.DryRun);
        if (settings.Json)
        {
            console.Json(new
            {
                schema_version = 1,
                path = result.Path,
                dry_run = result.DryRun,
                previous_template_version = result.PreviousTemplateVersion,
                template_version = result.TemplateVersion,
                changes = result.Changes,
                preserved_differences = result.PreservedDifferences
            });
        }
        else if (!settings.Quiet)
        {
            foreach (var change in result.Changes)
                console.Out.WriteLine($"{(result.DryRun ? "Would set" : "Set")} {change.Key}: {change.PreviousValue ?? "<missing>"} -> {change.Value}");
            foreach (var difference in result.PreservedDifferences)
                console.Out.WriteLine($"Preserved {difference.Key} = {difference.CurrentValue} ({difference.Reason})");
            if (result.Changes.Count == 0)
                console.Out.WriteLine("Configuration already uses the current template defaults.");
        }
        return ExitCodes.Success;
    }
}
