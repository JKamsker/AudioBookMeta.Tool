using System.Diagnostics;
using System.Text.Json;

namespace AudiobookMeta.Tool.Tests;

public sealed class CliContractTests
{
    [Fact]
    public async Task Top_level_help_succeeds_and_shows_stable_tree()
    {
        var result = await RunAsync("--help");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("dotnet audiobookmeta", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("search", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("author", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("providers", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("config", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Completion_targets_the_dotnet_audiobookmeta_subcommand()
    {
        var result = await RunAsync("completion", "bash");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("audiobookmeta", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("complete -F _audiobookmeta_dotnet dotnet", result.Stdout, StringComparison.Ordinal);
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
        Assert.DoesNotContain("Diagnostic log saved", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_and_jsonl_are_mutually_exclusive()
    {
        var result = await RunAsync("search", "dune", "--json", "--jsonl", "--config", SampleConfig());
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Stdout);
    }

    [Fact]
    public async Task Native_page_rejects_providers_that_cannot_honor_it()
    {
        var result = await RunAsync("search", "dune", "--page", "1", "--config", SampleConfig());
        Assert.Equal(6, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("requires native pagination", result.Stderr, StringComparison.Ordinal);
        Assert.Contains("--provider libex", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Author_books_help_documents_provider_native_lookup()
    {
        var result = await RunAsync("author", "books", "--help");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("<NAME>", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--provider", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--region", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("--json", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_help_marks_shop_link_hydration_as_explicit_and_slow()
    {
        var result = await RunAsync("search", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--shop-links", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("slower", result.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task First_run_creates_platform_default_config_with_public_providers()
    {
        var directory = TemporaryDirectory();
        var configHome = Path.Combine(directory, "config");
        var result = await RunWithEnvironmentAsync(
            new Dictionary<string, string?> { ["XDG_CONFIG_HOME"] = configHome, ["BOOKMETA_CONFIG"] = null },
            "providers", "list", "--no-color");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("libex", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("audiosilo", result.Stdout, StringComparison.Ordinal);
        var path = Path.Combine(configHome, "bookmeta", "config.toml");
        Assert.True(File.Exists(path));
        Assert.Contains("default_group = \"default\"", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_explicit_config_remains_an_error_for_read_commands()
    {
        var path = Path.Combine(TemporaryDirectory(), "missing.toml");
        var result = await RunAsync("providers", "list", "--config", path);

        Assert.Equal(3, result.ExitCode);
        Assert.False(File.Exists(path));
        Assert.Contains("Configuration file was not found", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Config_commands_create_edit_read_and_validate_a_custom_file()
    {
        var path = Path.Combine(TemporaryDirectory(), "nested", "config.toml");
        var set = await RunAsync("config", "set", "search.limit", "25", "--config", path, "--json");
        Assert.Equal(0, set.ExitCode);
        using (var json = JsonDocument.Parse(set.Stdout))
        {
            Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
            Assert.Equal(25, json.RootElement.GetProperty("value").GetInt32());
        }

        var get = await RunAsync("config", "get", "search.limit", "--config", path, "--json");
        Assert.Equal(0, get.ExitCode);
        using (var json = JsonDocument.Parse(get.Stdout))
            Assert.Equal(25, json.RootElement.GetProperty("value").GetInt32());

        var validate = await RunAsync("config", "validate", "--config", path);
        Assert.Equal(0, validate.ExitCode);
    }

    [Fact]
    public async Task Config_unset_requires_confirmation_and_supports_dry_run()
    {
        var path = SampleConfig();
        var refused = await RunAsync("config", "unset", "providers.goodreads", "--config", path);
        Assert.Equal(2, refused.ExitCode);

        var preview = await RunAsync("config", "unset", "providers.goodreads", "--config", path, "--dry-run", "--json");
        Assert.Equal(0, preview.ExitCode);
        using (var json = JsonDocument.Parse(preview.Stdout))
        {
            Assert.True(json.RootElement.GetProperty("dry_run").GetBoolean());
            Assert.False(json.RootElement.GetProperty("removed").GetBoolean());
        }
        Assert.Contains("[providers.goodreads]", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), StringComparison.Ordinal);

        var confirmed = await RunAsync("config", "unset", "providers.goodreads", "--config", path, "--yes");
        Assert.Equal(0, confirmed.ExitCode);
        Assert.DoesNotContain("[providers.goodreads]", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Config_get_redacts_secret_values()
    {
        var path = SampleConfig();
        var set = await RunAsync("config", "set", "providers.libex.auth", "literal:do-not-print", "--config", path);
        Assert.Equal(0, set.ExitCode);
        Assert.DoesNotContain("do-not-print", set.Stdout, StringComparison.Ordinal);

        var get = await RunAsync("config", "get", "providers.libex.auth", "--config", path, "--json");
        Assert.Equal(0, get.ExitCode);
        Assert.DoesNotContain("do-not-print", get.Stdout, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(get.Stdout);
        Assert.Equal("<redacted>", json.RootElement.GetProperty("value").GetString());
    }

    private static async Task<ProcessResult> RunAsync(params string[] arguments)
        => await RunWithEnvironmentAsync(new Dictionary<string, string?>(), arguments);

    private static async Task<ProcessResult> RunWithEnvironmentAsync(
        IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var root = RepositoryRoot();
        var assembly = Path.Combine(AppContext.BaseDirectory, "dotnet-audiobookmeta.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root
        };
        foreach (var (name, value) in environment)
        {
            if (value is null)
                start.Environment.Remove(name);
            else
                start.Environment[name] = value;
        }
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start audiobookmeta process.");
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string SampleConfig()
    {
        var source = Path.Combine(RepositoryRoot(), "docs", "tasks", "initial", "bookmeta-cli-spec", "examples", "config.toml");
        var directory = Path.Combine(Path.GetTempPath(), $"audiobookmeta-tool-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "config.toml");
        File.Copy(source, target);
        return target;
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audiobookmeta-tool-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AudioBookMeta.Tool.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
