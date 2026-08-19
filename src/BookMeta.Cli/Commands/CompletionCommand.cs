using System.ComponentModel;
using BookMeta.Common;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BookMeta.Commands;

public sealed class CompletionSettings : CommandSettings
{
    [CommandArgument(0, "<SHELL>")]
    [Description("bash, zsh, fish, or powershell.")]
    public required string Shell { get; init; }

    public override ValidationResult Validate() => new[] { "bash", "zsh", "fish", "powershell" }.Contains(Shell, StringComparer.OrdinalIgnoreCase)
        ? ValidationResult.Success() : ValidationResult.Error("Shell must be bash, zsh, fish, or powershell.");
}

public sealed class CompletionCommand(AppConsole console) : Command<CompletionSettings>
{
    private const string Commands = "search get providers config completion";

    protected override int Execute(CommandContext context, CompletionSettings settings, CancellationToken cancellationToken)
    {
        var script = settings.Shell.ToLowerInvariant() switch
        {
            "bash" => $"_bookmeta() {{ COMPREPLY=( $(compgen -W \"{Commands}\" -- \"${{COMP_WORDS[1]}}\") ); }}\ncomplete -F _bookmeta bookmeta",
            "zsh" => $"#compdef bookmeta\n_arguments '1:command:({Commands})'",
            "fish" => string.Join('\n', Commands.Split(' ').Select(command => $"complete -c bookmeta -n '__fish_use_subcommand' -a '{command}'")),
            _ => $"Register-ArgumentCompleter -CommandName bookmeta -ScriptBlock {{ param($wordToComplete) '{Commands.Replace(" ", "','", StringComparison.Ordinal)}' | Where-Object {{ $_ -like \"$wordToComplete*\" }} }}"
        };
        console.Out.WriteLine(script);
        return ExitCodes.Success;
    }
}
