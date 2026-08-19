using System.Net;
using BookMeta.Configuration;
using BookMeta.Model;
using BookMeta.Providers;
using BookMeta.Providers.Abs;
using BookMeta.Providers.AudioSilo;
using BookMeta.Providers.Libex;

namespace BookMeta.Cli.Tests;

public sealed class AdapterTests
{
    [Fact]
    public async Task Abs_adapter_normalizes_minimal_and_complete_matches()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/search", request.RequestUri!.AbsolutePath);
            Assert.Contains("query=dune", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Json("""{"matches":[{"title":"Dune","author":"Frank Herbert","narrator":"Simon Vance","isbn":"9780441172719","duration":1234},{"title":"Minimal"}]}"""));
        });
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        var response = await provider.SearchAsync(new SearchRequest { Query = "dune" }, false, TestContext.Current.CancellationToken);
        Assert.Equal(2, response.Candidates.Count);
        Assert.Equal("Frank Herbert", response.Candidates[0].Authors[0]);
        Assert.Equal(1234, response.Candidates[0].DurationSeconds);
    }

    [Fact]
    public async Task Libex_kiota_client_deserializes_search_fixture()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            Assert.Contains(request.RequestUri!.AbsolutePath, new[] { "/quick-search", "/search" });
            return Task.FromResult(TestHttpFactory.Json("""[{"asin":"B08G9PRS1K","title":"Project Hail Mary","region":"us","authors":[{"name":"Andy Weir"}],"narrators":[{"name":"Ray Porter"}],"lengthMinutes":960}]"""));
        });
        var config = Config("libex");
        var provider = new LibexProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));
        var response = await provider.SearchAsync(new SearchRequest { Query = "proj hail", LimitPerProvider = 10 }, false, TestContext.Current.CancellationToken);
        Assert.Equal(2, requests);
        Assert.Equal("Project Hail Mary", response.Candidates[0].Title);
        Assert.Equal("Andy Weir", response.Candidates[0].Authors[0]);
    }

    [Fact]
    public async Task AudioSilo_kiota_client_filters_mixed_search_results()
    {
        var factory = new TestHttpFactory((request, _) => Task.FromResult(TestHttpFactory.Json("""
            {"results":[
              {"kind":"person","id":"andy-weir","name":"Andy Weir"},
              {"kind":"work","id":"project-hail-mary","title":"Project Hail Mary","authors":[{"id":"andy-weir","name":"Andy Weir"}],"series":null,"cover_url":null,"added_at":null,"narrators":[{"id":"ray-porter","name":"Ray Porter"}]}
            ]}
            """)));
        var config = Config("audiosilo");
        var provider = new AudioSiloProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));
        var response = await provider.SearchAsync(new SearchRequest { Query = "project hail" }, false, TestContext.Current.CancellationToken);
        Assert.Single(response.Candidates);
        Assert.Equal("Ray Porter", response.Candidates[0].Narrators[0]);
    }

    [Fact]
    public async Task AudioSilo_editions_expand_each_work_recording_with_bounded_detail_requests()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal)
                ? TestHttpFactory.Json("""{"results":[{"kind":"work","id":"the-work","title":"The Work","authors":[],"series":null,"narrators":[]}]}""")
                : TestHttpFactory.Json("""{"id":"the-work","title":"The Work","authors":[],"series":[],"recordings":[{"id":"first","isbn":["9780441172719"],"narrators":[]},{"id":"second","isbn":[],"narrators":[]}]}"""));
        });
        var config = Config("audiosilo");
        var provider = new AudioSiloProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));

        var response = await provider.SearchAsync(new SearchRequest { Query = "the work", Editions = true }, false, TestContext.Current.CancellationToken);

        Assert.Equal(2, requests);
        Assert.Equal(2, response.RequestCount);
        Assert.Collection(response.Candidates,
            first => Assert.Equal("work/the-work/recording/first", first.ProviderRecordId),
            second => Assert.Equal("work/the-work/recording/second", second.ProviderRecordId));
    }

    [Fact]
    public async Task Authenticated_cross_host_redirect_is_refused()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://evil.example/search") }
        }));
        var config = Config("abs") with { Auth = "literal:secret" };
        var provider = new AbsProvider(config, new ProviderTransport(factory));
        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.SearchAsync(new SearchRequest { Query = "x" }, false, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abs_documented_not_found_can_be_treated_as_empty()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{}", HttpStatusCode.NotFound)));
        var config = Config("abs") with
        {
            CapabilityOverrides = new Dictionary<string, CapabilityState> { ["not_found_is_empty"] = CapabilityState.Supported }
        };
        var response = await new AbsProvider(config, new ProviderTransport(factory))
            .SearchAsync(new SearchRequest { Query = "missing" }, false, TestContext.Current.CancellationToken);
        Assert.Empty(response.Candidates);
        Assert.Single(response.Warnings);
    }

    [Fact]
    public async Task Abs_malformed_matches_shape_is_rejected()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{\"matches\":{}}")));
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        var exception = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.SearchAsync(new SearchRequest { Query = "x" }, false, TestContext.Current.CancellationToken));
        Assert.Equal("invalid_response", exception.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "http_auth")]
    [InlineData(HttpStatusCode.TooManyRequests, "http_rate_limit")]
    [InlineData(HttpStatusCode.InternalServerError, "http_server")]
    public async Task Abs_http_failures_are_classified(HttpStatusCode status, string expectedKind)
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{}", status)));
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        var exception = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.SearchAsync(new SearchRequest { Query = "x" }, false, TestContext.Current.CancellationToken));
        Assert.Equal(expectedKind, exception.Kind);
    }

    [Fact]
    public async Task Oversized_provider_response_is_rejected_before_parsing()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[10 * 1024 * 1024 + 1])
        }));
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            provider.SearchAsync(new SearchRequest { Query = "x" }, true, TestContext.Current.CancellationToken));
    }

    private static ProviderConfig Config(string type) => new()
    {
        Id = type,
        Type = type,
        BaseUrl = new Uri("https://provider.example"),
        Enabled = true
    };
}
