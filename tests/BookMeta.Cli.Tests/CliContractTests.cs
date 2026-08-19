using System.Diagnostics;
using System.Text.Json;

namespace BookMeta.Cli.Tests;

public sealed class CliContractTests
{
    [Fact]
    public async Task Top_level_help_succeeds_and_shows_stable_tree()
    {
        var result = await RunAsync("--help");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("search", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("providers", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("config", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Representative_list_is_human_by_default_and_valid_json_on_request()
    {
        var config = SampleConfig();
        var human = await RunAsync("providers", "list", "--config", config, "--no-color");
        Assert.Equal(0, human.ExitCode);
        Assert.DoesNotMatch("^\\s*[\\[{]", human.Stdout);
        Assert.Contains("Caps", human.Stdout, StringComparison.Ordinal);

        var machine = await RunAsync("providers", "list", "--config", config, "--json");
        Assert.Equal(0, machine.ExitCode);
        using var json = JsonDocument.Parse(machine.Stdout);
        Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("providers").ValueKind);
    }

    [Fact]
    public async Task Missing_search_input_is_usage_error()
    {
        var result = await RunAsync("search", "--config", SampleConfig());
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("Supply QUERY", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_and_jsonl_are_mutually_exclusive()
    {
        var result = await RunAsync("search", "dune", "--json", "--jsonl", "--config", SampleConfig());
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
    }

    private static async Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var root = RepositoryRoot();
        var assembly = Path.Combine(root, "src", "BookMeta.Cli", "bin", "Debug", "net10.0", "bookmeta.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root
        };
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start bookmeta process.");
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string SampleConfig()
    {
        var source = Path.Combine(RepositoryRoot(), "docs", "tasks", "initial", "bookmeta-cli-spec", "examples", "config.toml");
        var directory = Path.Combine(Path.GetTempPath(), $"bookmeta-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "config.toml");
        File.Copy(source, target);
        return target;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BookMeta.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
