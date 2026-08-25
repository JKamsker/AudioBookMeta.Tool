using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigPathSettings : GlobalSettings;

public sealed class ConfigPathCommand(ConfigPathResolver resolver, AppConsole console) : Command<ConfigPathSettings>
{
    protected override int Execute(CommandContext context, ConfigPathSettings settings, CancellationToken cancellationToken)
    {
        var path = resolver.Resolve(settings.ConfigPath);
        if (settings.Json)
            console.Json(new { schema_version = 1, path });
        else
            console.Out.WriteLine(path);
        return ExitCodes.Success;
    }
}
