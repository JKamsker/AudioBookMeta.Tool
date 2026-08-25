using System.Diagnostics;
using System.Net;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Search;

namespace AudiobookMeta.Tool.Tests;

public sealed class SearchEngineTests
{
    [Fact]
    public async Task Providers_execute_concurrently()
    {
        var factory = new TestHttpFactory(async (request, cancellationToken) =>
        {
            await Task.Delay(250, cancellationToken);
            return TestHttpFactory.Json($$"""{"matches":[{"title":"{{request.RequestUri!.Host}}"}]}""");
        });
        var engine = Engine(factory);
        var timer = Stopwatch.StartNew();
        var execution = await engine.ExecuteAsync(Config(), new SearchRequest { Query = "book" }, Options(strict: false), TestContext.Current.CancellationToken);
        Assert.Equal(ExitCodes.Success, execution.ExitCode);
        Assert.Equal(2, execution.Response.Results.Count);
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(450), $"Expected concurrent execution, elapsed {timer.Elapsed}.");
    }

    [Fact]
    public async Task Strict_mode_changes_exit_code_but_preserves_successful_results()
    {
        var factory = new TestHttpFactory((request, _) => Task.FromResult(request.RequestUri!.Host == "bad.example"
            ? TestHttpFactory.Json("{}", HttpStatusCode.InternalServerError)
            : TestHttpFactory.Json("""{"matches":[{"title":"Dune","author":"Frank Herbert"}]}""")));
        var engine = Engine(factory);
        var normal = await engine.ExecuteAsync(Config(), new SearchRequest { Query = "dune" }, Options(strict: false), TestContext.Current.CancellationToken);
        var strict = await engine.ExecuteAsync(Config(), new SearchRequest { Query = "dune" }, Options(strict: true), TestContext.Current.CancellationToken);
        Assert.Equal(ExitCodes.Success, normal.ExitCode);
        Assert.Equal(ExitCodes.StrictFailure, strict.ExitCode);
        Assert.Single(strict.Response.Results);
        Assert.Contains(strict.Response.ProviderStatus, status => status.Status == "error");
    }

    [Fact]
    public async Task Editions_apply_limit_to_individual_candidates()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("""
            {"matches":[
              {"id":"one","title":"Dune","author":"Frank Herbert","narrator":"Narrator One"},
              {"id":"two","title":"Dune","author":"Frank Herbert","narrator":"Narrator Two"}
            ]}
            """)));
        var execution = await Engine(factory).ExecuteAsync(Config(),
            new SearchRequest { Query = "dune", Editions = true },
            Options(strict: false) with { Limit = 1 }, TestContext.Current.CancellationToken);
        Assert.Single(execution.Response.Results);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_provider_failure()
    {
        var factory = new TestHttpFactory(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return TestHttpFactory.Json("{\"matches\":[]}");
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Engine(factory).ExecuteAsync(
            Config(), new SearchRequest { Query = "book" }, Options(strict: false), cancellation.Token));
    }

    private static SearchEngine Engine(IHttpClientFactory http)
    {
        var transport = new ProviderTransport(http);
        return new SearchEngine(new ProviderSelector(), new ProviderFactory(transport, new KiotaClientFactory(http)),
            new SearchCache(new ConfigPathResolver()), new ResultRanker(), new ResultClusterer());
    }

    private static AudiobookMetaConfig Config() => new()
    {
        SourcePath = "test.toml",
        Search = new SearchConfig { MaxConcurrency = 2, ProviderTimeout = TimeSpan.FromSeconds(2), Deadline = TimeSpan.FromSeconds(2) },
        Providers = new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["good"] = Provider("good", "https://good.example"),
            ["bad"] = Provider("bad", "https://bad.example")
        }
    };

    private static ProviderConfig Provider(string id, string url) => new() { Id = id, Type = "abs", BaseUrl = new Uri(url), Enabled = true };

    private static SearchOptions Options(bool strict) => new()
    {
        Includes = [],
        Groups = [],
        Excludes = [],
        Fresh = true,
        IncludeRaw = true,
        Strict = strict
    };
}
