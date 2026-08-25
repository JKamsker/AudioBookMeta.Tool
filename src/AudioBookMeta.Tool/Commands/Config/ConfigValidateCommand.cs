using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Config;

public sealed class ConfigValidateSettings : GlobalSettings;

public sealed class ConfigValidateCommand(ConfigLoader loader, AppConsole console) : Command<ConfigValidateSettings>
{
    protected override int Execute(CommandContext context, ConfigValidateSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        ConfigValidator.ThrowIfInvalid(config, resolveSecrets: true);
        var plaintext = config.Providers.Values.Where(provider => provider.Auth?.StartsWith("literal:", StringComparison.Ordinal) == true).Select(provider => provider.Id).ToList();
        var warnings = plaintext.Select(id => $"provider '{id}' stores an auth value as plaintext literal").ToList();
        if (settings.Json)
            console.Json(new { schema_version = 1, valid = true, path = config.SourcePath, providers = config.Providers.Count, groups = config.Groups.Count, warnings });
        else
        {
            console.Out.WriteLine($"Configuration is valid: {config.SourcePath}");
            foreach (var warning in warnings)
                console.Warning(warning);
        }
        return ExitCodes.Success;
    }
}
