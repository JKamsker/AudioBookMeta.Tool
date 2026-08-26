using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigStatusSettings : GlobalSettings;

public sealed class ConfigStatusCommand(ConfigMigrationService migrations, AppConsole console) : Command<ConfigStatusSettings>
{
    protected override int Execute(CommandContext context, ConfigStatusSettings settings, CancellationToken cancellationToken)
    {
        var status = migrations.Status(settings.ConfigPath);
        if (settings.Json)
        {
            console.Json(new
            {
                schema_version = 1,
                path = status.Path,
                recorded_template_version = status.RecordedTemplateVersion,
                current_template_version = status.CurrentTemplateVersion,
                up_to_date = status.RecordedTemplateVersion == status.CurrentTemplateVersion && status.Differences.Count == 0,
                differences = status.Differences
            });
        }
        else if (!settings.Quiet)
        {
            console.Out.WriteLine($"Configuration: {status.Path}");
            console.Out.WriteLine($"Template: {status.RecordedTemplateVersion?.ToString() ?? "unversioned"} (current {status.CurrentTemplateVersion})");
            if (status.Differences.Count == 0)
            {
                console.Out.WriteLine("No provider defaults differ from the current template.");
            }
            else
            {
                var table = new Table().Border(TableBorder.Simple)
                    .AddColumn("Key").AddColumn("Current").AddColumn("Default").AddColumn("Classification");
                foreach (var difference in status.Differences)
                    table.AddRow(
                        AppConsole.Safe(difference.Key),
                        AppConsole.Safe(difference.CurrentValue),
                        AppConsole.Safe(difference.DefaultValue),
                        difference.GeneratedDefaultCandidate ? "generated default candidate" : "custom value (preserved)");
                console.Out.Write(table);
            }
        }
        return ExitCodes.Success;
    }
}
