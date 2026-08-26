using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.Audible;

public sealed class AudibleProvider(ProviderConfig config, ProviderTransport transport) : IMetadataProvider
{
    private const string ResponseGroups = "contributors,product_desc,product_extended_attrs,rating,series,sku";

    public string Id => config.Id;
    public string AdapterType => "audible";
    public ProviderCapabilities Capabilities { get; } = CapabilityCatalog.Create(config,
        "search", "free_text_query", "title_filter", "author_filter", "narrator_filter", "publisher_filter",
        "asin_filter", "sku_filter", "duration_metadata", "region_filter", "get_by_id", "health");

    public async Task<ProviderSearchResponse> SearchAsync(SearchRequest request, bool includeRaw, CancellationToken cancellationToken)
    {
        var marketplace = Marketplace(request.Region);
        if (!string.IsNullOrWhiteSpace(request.Asin))
        {
            try
            {
                var result = await GetCoreAsync(request.Asin, marketplace, includeRaw, cancellationToken);
                return new() { Candidates = [result], RequestCount = 1, LookupStrategies = ["direct_asin"] };
            }
            catch (ProviderException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound || exception.Kind == "not_found")
            {
                return new() { RequestCount = 1, LookupStrategies = ["direct_asin"] };
            }
        }

        var keywords = SearchText(request);
        var uri = ProviderTransport.BuildUri(BaseUrl(marketplace), "1.0/catalog/products",
        [
            new("keywords", keywords),
            new("num_results", Math.Min(request.LimitPerProvider, 50).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("products_sort_by", "Relevance"),
            new("response_groups", ResponseGroups)
        ]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = Parse(response.Content);
        if (!document.RootElement.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
            throw new ProviderException(Id, "invalid_response", "Audible catalogue response has no products array");
        var mapper = Mapper(marketplace);
        var candidates = products.EnumerateArray().Select(item => mapper.Map(item, includeRaw, "catalog_search"))
            .Where(item => item is not null).Select(item => item!).ToList();
        return new() { Candidates = candidates, RequestCount = 1, LookupStrategies = ["catalog_search"] };
    }

    public Task<SearchResult> GetAsync(string id, string? region, bool includeRaw, CancellationToken cancellationToken)
        => GetCoreAsync(id, Marketplace(region), includeRaw, cancellationToken);

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var marketplace = Marketplace(null);
        var uri = ProviderTransport.BuildUri(BaseUrl(marketplace), "1.0/catalog/products",
        [
            new("keywords", "Dune"),
            new("num_results", "1"),
            new("response_groups", "contributors,sku")
        ]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = Parse(response.Content);
        if (!document.RootElement.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
            throw new ProviderException(Id, "invalid_response", "Audible catalogue health response has no products array");
        return new(Id, "ok", timer.ElapsedMilliseconds, $"Audible {marketplace} catalogue is reachable and structurally valid");
    }

    private async Task<SearchResult> GetCoreAsync(string id, string marketplace, bool includeRaw, CancellationToken cancellationToken)
    {
        var asin = NormalizeAsin(id);
        var uri = ProviderTransport.BuildUri(BaseUrl(marketplace), $"1.0/catalog/products/{Uri.EscapeDataString(asin)}",
        [new("response_groups", ResponseGroups)]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = Parse(response.Content);
        if (!document.RootElement.TryGetProperty("product", out var product) || product.ValueKind != JsonValueKind.Object)
            throw new ProviderException(Id, "invalid_response", $"Audible product response for '{asin}' in marketplace '{marketplace}' has no product object");
        return Mapper(marketplace).Map(product, includeRaw, "direct_asin")
            ?? throw new ProviderException(Id, "invalid_response", "Audible product response is missing ASIN or title");
    }

    private AudibleModelMapper Mapper(string marketplace) => new(Id, marketplace, AudibleMarketplace.Domain(marketplace));

    private Uri BaseUrl(string marketplace)
    {
        if (!config.BaseUrl.Host.StartsWith("api.audible.", StringComparison.OrdinalIgnoreCase))
            return config.BaseUrl;
        var builder = new UriBuilder(config.BaseUrl) { Host = $"api.audible.{AudibleMarketplace.Domain(marketplace)}" };
        return builder.Uri;
    }

    private string Marketplace(string? region)
    {
        if (AudibleMarketplace.TryNormalize(region ?? config.Region, out var marketplace))
            return marketplace;
        throw new ProviderException(Id, "invalid_region", $"Unsupported Audible marketplace '{region ?? config.Region}'");
    }

    private string NormalizeAsin(string value)
    {
        var asin = new string(value.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (asin.Length != 10)
            throw new ProviderException(Id, "invalid_identifier", "Audible ASIN must contain exactly 10 letters or digits");
        return asin;
    }

    private static string SearchText(SearchRequest request) => string.Join(' ', new[]
    {
        request.Query, request.Title, request.Author, request.Narrator, request.Series, request.Sku, request.Publisher
    }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

    private JsonDocument Parse(byte[] content)
    {
        try { return JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException exception) { throw new ProviderException(Id, "invalid_response", "Audible returned invalid JSON", inner: exception); }
    }
}
