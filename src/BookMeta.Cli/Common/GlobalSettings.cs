using System.ComponentModel;
using Spectre.Console.Cli;

namespace BookMeta.Common;

public abstract class GlobalSettings : CommandSettings
{
    [CommandOption("--config <PATH>")]
    [Description("Use this TOML configuration file (overrides BOOKMETA_CONFIG).")]
    public string? ConfigPath { get; init; }

    [CommandOption("--json")]
    [Description("Emit the stable JSON v1 machine contract.")]
    public bool Json { get; init; }

    [CommandOption("--quiet")]
    [Description("Suppress non-essential human output; never suppress machine output.")]
    public bool Quiet { get; init; }

    [CommandOption("--no-color")]
    [Description("Disable ANSI color (NO_COLOR is also respected).")]
    public bool NoColor { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Show resolved settings and diagnostic details on stderr.")]
    public bool Verbose { get; init; }
}
