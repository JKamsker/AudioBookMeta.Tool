using System.Net;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Providers.Audible;

namespace AudiobookMeta.Tool.Tests;

public sealed class AudibleProviderTests
{
    [Fact]
    public async Task Search_maps_catalogue_metadata_and_marketplace_provenance()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("api.audible.es", request.RequestUri!.Host);
            Assert.Equal("/1.0/catalog/products", request.RequestUri.AbsolutePath);
            Assert.Contains("keywords=Marina%20Grit%20Landau", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Json("""
                {"total_results":1,"products":[{"asin":"B084WH8CFN","title":"Marina, Marina",
                  "authors":[{"name":"Grit Landau"}],"narrators":[{"name":"Steffen Groth"}],
                  "runtime_length_min":630,"publisher_name":"Argon Verlag","publication_datetime":"2020-02-13T00:00:00Z",
                  "language":"spanish","sku":"BK_ARGO_002245ES","sku_lite":"BK_ARGO_002245",
                  "product_images":{"500":"https://images.example/500.jpg"},
                  "series":[{"title":"Marina","sequence":"1"}]}]}
                """));
        });
        var provider = Provider(factory);

        var response = await provider.SearchAsync(new SearchRequest
        {
            Title = "Marina",
            Author = "Grit Landau",
            Region = "es"
        }, false, TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Candidates);
        Assert.Equal("B084WH8CFN", result.ProviderRecordId);
        Assert.Equal("Grit Landau", Assert.Single(result.Authors));
        Assert.Equal("Steffen Groth", Assert.Single(result.Narrators));
        Assert.Equal(37800, result.DurationSeconds);
        Assert.Equal("BK_ARGO_002245ES", result.Identifiers.Other["sku"]);
        Assert.Equal("BK_ARGO_002245", result.Identifiers.Other["skuGroup"]);
        Assert.Equal("es", Assert.Single(result.Regions));
        Assert.Equal("catalog_search", result.LookupStrategy);
        Assert.Contains(result.IdentifierProvenance, source => source.Source == "audible_catalog:es");
        Assert.Equal(CapabilityState.Supported, provider.Capabilities["asin_filter"]);
        Assert.Equal(CapabilityState.Supported, provider.Capabilities["sku_filter"]);
        Assert.Equal(CapabilityState.Supported, provider.Capabilities["duration_metadata"]);
    }

    [Fact]
    public async Task Direct_get_uses_marketplace_product_route()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("api.audible.es", request.RequestUri!.Host);
            Assert.Equal("/1.0/catalog/products/B084WH8CFN", request.RequestUri.AbsolutePath);
            return Task.FromResult(TestHttpFactory.Json("""
                {"product":{"asin":"B084WH8CFN","title":"Marina, Marina","authors":[{"name":"Grit Landau"}]}}
                """));
        });

        var result = await Provider(factory).GetAsync("b084wh8cfn", "es", true, TestContext.Current.CancellationToken);

        Assert.Equal("B084WH8CFN", result.ProviderRecordId);
        Assert.Equal("direct_asin", result.LookupStrategy);
        Assert.Equal("es", Assert.Single(result.Regions));
        Assert.NotNull(result.Raw);
    }

    [Fact]
    public async Task Direct_search_distinguishes_delisted_from_connectivity_failure()
    {
        var notFound = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{}", HttpStatusCode.NotFound)));
        var response = await Provider(notFound).SearchAsync(
            new SearchRequest { Asin = "B084WH8CFN" }, false, TestContext.Current.CancellationToken);

        Assert.Empty(response.Candidates);
        Assert.Equal(["direct_asin"], response.LookupStrategies);

        var failure = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{}", HttpStatusCode.ServiceUnavailable)));
        var exception = await Assert.ThrowsAsync<ProviderException>(() => Provider(failure).SearchAsync(
            new SearchRequest { Asin = "B084WH8CFN" }, false, TestContext.Current.CancellationToken));
        Assert.Equal("http_server", exception.Kind);
    }

    [Fact]
    public async Task Empty_search_is_valid_but_a_malformed_shape_is_not()
    {
        var empty = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{\"products\":[],\"total_results\":0}")));
        var response = await Provider(empty).SearchAsync(
            new SearchRequest { Query = "missing" }, false, TestContext.Current.CancellationToken);
        Assert.Empty(response.Candidates);

        var malformed = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{\"products\":{}}")));
        var exception = await Assert.ThrowsAsync<ProviderException>(() => Provider(malformed).SearchAsync(
            new SearchRequest { Query = "changed" }, false, TestContext.Current.CancellationToken));
        Assert.Equal("invalid_response", exception.Kind);
    }

    private static AudibleProvider Provider(IHttpClientFactory http) => new(new ProviderConfig
    {
        Id = "audible-de",
        Type = "audible",
        BaseUrl = new Uri("https://api.audible.de"),
        Region = "de",
        Enabled = true
    }, new ProviderTransport(http));
}
