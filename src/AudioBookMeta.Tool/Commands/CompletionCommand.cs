using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands;

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
    private const string Commands = "search get author providers config completion";

    protected override int Execute(CommandContext context, CompletionSettings settings, CancellationToken cancellationToken)
    {
        var script = settings.Shell.ToLowerInvariant() switch
        {
            "bash" => $"_audiobookmeta_dotnet() {{ [ \"${{COMP_WORDS[1]}}\" = \"audiobookmeta\" ] || return; COMPREPLY=( $(compgen -W \"{Commands}\" -- \"${{COMP_WORDS[COMP_CWORD]}}\") ); }}\ncomplete -F _audiobookmeta_dotnet dotnet",
            "zsh" => $"_audiobookmeta_dotnet() {{ [[ $words[2] == audiobookmeta ]] || return; _values 'command' {Commands}; }}\ncompdef _audiobookmeta_dotnet dotnet",
            "fish" => string.Join('\n', Commands.Split(' ').Select(command => $"complete -c dotnet -n '__fish_seen_subcommand_from audiobookmeta' -a '{command}'")),
            _ => $"Register-ArgumentCompleter -CommandName dotnet -ScriptBlock {{ param($wordToComplete, $commandAst) if ($commandAst.ToString() -like 'dotnet audiobookmeta*') {{ '{Commands.Replace(" ", "','", StringComparison.Ordinal)}' | Where-Object {{ $_ -like \"$wordToComplete*\" }} }} }}"
        };
        console.Out.WriteLine(script);
        return ExitCodes.Success;
    }
}
