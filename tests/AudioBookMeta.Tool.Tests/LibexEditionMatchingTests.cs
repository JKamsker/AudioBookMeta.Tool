using System.Net;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Providers.Libex;

namespace AudiobookMeta.Tool.Tests;

public sealed class LibexEditionMatchingTests
{
    [Fact]
    public async Task Sku_lookup_uses_native_endpoint_and_preserves_identifiers()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/book/sku/BK_HOER_002668", request.RequestUri!.AbsolutePath);
            return Task.FromResult(TestHttpFactory.Json("""
                [{"asin":"3844533796","title":"Bertrams Hotel","sku":"BK_HOER_002668","skuGroup":"BK_HOER_002668","lengthMinutes":377}]
                """));
        });
        var provider = Provider(factory);

        var response = await provider.SearchAsync(
            new SearchRequest { Sku = "BK_HOER_002668" },
            false,
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Candidates);
        Assert.Equal("3844533796", result.ProviderRecordId);
        Assert.Equal("3844533796", Assert.Single(result.Identifiers.Asin));
        Assert.Equal("BK_HOER_002668", result.Identifiers.Other["sku"]);
        Assert.Equal("BK_HOER_002668", result.Identifiers.Other["skuGroup"]);
        Assert.Equal("sku", result.LookupStrategy);
        Assert.Equal(["sku"], response.LookupStrategies);
        Assert.Equal(CapabilityState.Supported, provider.Capabilities["sku_lookup"]);
    }

    [Fact]
    public async Task Empty_structured_search_falls_back_to_native_author_books()
    {
        var requests = new List<string>();
        var factory = new TestHttpFactory((request, _) =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(request.RequestUri.AbsolutePath switch
            {
                "/quick-search" => TestHttpFactory.Json("[]"),
                "/search" => TestHttpFactory.Json("{}", HttpStatusCode.NotFound),
                "/author/books" => TestHttpFactory.Json("""
                    [{"asin":"B07PP8SSY5","title":"Prof. Kulls Blutnixe","authors":[{"name":"A. F. Morland"}]}]
                    """),
                _ => TestHttpFactory.Json("{}", HttpStatusCode.NotFound)
            });
        });
        var provider = Provider(factory);

        var response = await provider.SearchAsync(new SearchRequest
        {
            Title = "Prof. Kulls Blutnixe",
            Author = "A. F. Morland"
        }, false, TestContext.Current.CancellationToken);

        Assert.Equal(["/quick-search", "/search", "/author/books"], requests);
        var result = Assert.Single(response.Candidates);
        Assert.Equal("B07PP8SSY5", result.ProviderRecordId);
        Assert.Equal("author_fallback", result.LookupStrategy);
        Assert.Contains("author_fallback", response.LookupStrategies);
        Assert.Equal(3, response.RequestCount);
    }

    [Fact]
    public async Task Search_and_fallback_not_found_responses_are_empty_not_failures()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(
            TestHttpFactory.Json("{}", HttpStatusCode.NotFound)));
        var provider = Provider(factory);

        var response = await provider.SearchAsync(new SearchRequest
        {
            Title = "Missing title",
            Author = "Missing author"
        }, false, TestContext.Current.CancellationToken);

        Assert.Empty(response.Candidates);
        Assert.Equal(3, response.RequestCount);
        Assert.Equal(["quick_search", "structured_search", "author_fallback"], response.LookupStrategies);
        Assert.Contains(response.Warnings, warning => warning.Contains("HTTP 404", StringComparison.Ordinal));
    }

    private static LibexProvider Provider(IHttpClientFactory http)
    {
        var config = new ProviderConfig
        {
            Id = "libex",
            Type = "libex",
            BaseUrl = new Uri("https://provider.example"),
            Enabled = true
        };
        return new LibexProvider(config, new ProviderTransport(http), new KiotaClientFactory(http));
    }
}
