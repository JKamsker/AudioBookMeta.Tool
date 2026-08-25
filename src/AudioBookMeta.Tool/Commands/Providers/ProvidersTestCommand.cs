using System.ComponentModel;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AudiobookMeta.Tool.Commands.Providers;

public sealed class ProvidersTestSettings : GlobalSettings
{
    [CommandArgument(0, "[PROVIDER]")]
    [Description("Provider IDs; omit to test all enabled providers.")]
    public string[] Providers { get; init; } = [];
    [CommandOption("--timeout <DURATION>")]
    public string Timeout { get; init; } = "3s";

    public override ValidationResult Validate() => DurationParser.TryParse(Timeout, out _) ? ValidationResult.Success() : ValidationResult.Error("--timeout must be a positive duration.");
}

public sealed class ProvidersTestCommand(ConfigLoader loader, ProviderFactory factory, AppConsole console) : AsyncCommand<ProvidersTestSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ProvidersTestSettings settings, CancellationToken cancellationToken)
    {
        var config = loader.Load(settings.ConfigPath);
        var selected = settings.Providers.Length == 0 ? config.Providers.Values.Where(provider => provider.Enabled).ToList() : settings.Providers.Select(id => config.Providers.TryGetValue(id, out var provider) ? provider : throw new AudiobookMetaException($"Unknown provider '{id}'.", ExitCodes.Configuration)).ToList();
        if (selected.Count == 0)
            throw new AudiobookMetaException("No providers selected for testing.", ExitCodes.Configuration, "Enable or explicitly select a provider.");
        DurationParser.TryParse(settings.Timeout, out var timeout);
        var tasks = selected.Select(async provider =>
        {
            using var perProvider = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perProvider.CancelAfter(timeout);
            try { return await factory.Create(provider).TestAsync(perProvider.Token); }
            catch (OperationCanceledException) { return new ProviderTestResult(provider.Id, "timeout", (long)timeout.TotalMilliseconds, "provider test timed out"); }
            catch (ProviderException exception) { return new ProviderTestResult(provider.Id, exception.Status, 0, exception.Message); }
        });
        var results = await Task.WhenAll(tasks);
        if (settings.Json)
            console.Json(new { schema_version = 1, results });
        else
        {
            var table = new Table().Border(TableBorder.Simple).AddColumn("Provider").AddColumn("Status").AddColumn("Elapsed").AddColumn("Message");
            foreach (var result in results)
                table.AddRow(AppConsole.Safe(result.Provider), result.Status, $"{result.ElapsedMs}ms", AppConsole.Safe(result.Message));
            console.Out.Write(table);
        }
        return results.All(result => result.Status == "ok") ? ExitCodes.Success : ExitCodes.ProvidersFailed;
    }
}
