using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.Libex;

public sealed class LibexProvider(ProviderConfig config, ProviderTransport transport, KiotaClientFactory kiota)
    : IMetadataProvider, IAuthorBooksProvider
{
    private readonly AudiobookMeta.Tool.Generated.Libex.LibexApiClient client = kiota.CreateLibex(config);
    private readonly LibexModelMapper mapper = new(config.Id, config.Region);
    public string Id => config.Id;
    public string AdapterType => "libex";
    public ProviderCapabilities Capabilities { get; } = CapabilityCatalog.Create(config,
        "search", "free_text_query", "title_filter", "author_filter", "narrator_filter", "region_filter", "quick_search",
        "asin_filter", "sku_lookup", "duration_metadata", "author_fallback", "get_by_id", "bulk_get", "chapters",
        "author_search", "series_search", "native_sort", "native_pagination", "health");

    public async Task<ProviderSearchResponse> SearchAsync(SearchRequest request, bool includeRaw, CancellationToken cancellationToken)
    {
        if (!includeRaw)
            return await SearchTypedAsync(request, cancellationToken);
        return await SearchRawAsync(request, cancellationToken);
    }

    private async Task<ProviderSearchResponse> SearchRawAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Sku))
        {
            var results = await GetBySkuAsync(request.Sku, true, cancellationToken);
            return new ProviderSearchResponse { Candidates = results, RequestCount = 1, LookupStrategies = ["sku"] };
        }
        if (!string.IsNullOrWhiteSpace(request.Asin))
        {
            var result = (await GetRawAsync(request.Asin, request.Region, cancellationToken)) with { LookupStrategy = "asin" };
            return new ProviderSearchResponse { Candidates = [result], RequestCount = 1, LookupStrategies = ["asin"] };
        }

        var candidates = new List<SearchResult>();
        var warnings = new List<string>();
        var strategies = new List<string>();
        var requestCount = 0;
        var text = request.Query ?? request.Title;
        if (request.Page is null && !request.Exact && !string.IsNullOrWhiteSpace(text))
        {
            var quickUri = ProviderTransport.BuildUri(config.BaseUrl, "quick-search",
            [
                new("keywords", text), new("region", request.Region ?? config.Region)
            ]);
            requestCount++;
            strategies.Add("quick_search");
            try
            {
                var quick = await transport.GetAsync(config, quickUri, cancellationToken);
                candidates.AddRange(ParseBooks(quick.Content, true).Select(result => result with { LookupStrategy = "quick_search" }));
            }
            catch (ProviderException exception) when (IsNotFound(exception))
            {
                warnings.Add("quick search returned no results (HTTP 404)");
            }
        }

        if (requestCount == 0 || candidates.Count < request.LimitPerProvider || HasStructuredHints(request))
        {
            var uri = ProviderTransport.BuildUri(config.BaseUrl, "search",
            [
                new("title", request.Title), new("author", request.Author), new("narrator", request.Narrator),
                new("query", request.Query), new("keywords", request.Query), new("limit", Math.Min(request.LimitPerProvider, LibexDefaults.MaximumSearchLimit).ToString()),
                new("page", (request.Page ?? LibexDefaults.DefaultSearchPage).ToString()), new("cache", "false"), new("region", request.Region ?? config.Region)
            ]);
            requestCount++;
            strategies.Add("structured_search");
            try
            {
                var search = await transport.GetAsync(config, uri, cancellationToken);
                candidates.AddRange(ParseBooks(search.Content, true).Select(result => result with { LookupStrategy = "structured_search" }));
            }
            catch (ProviderException exception) when (IsNotFound(exception))
            {
                warnings.Add("structured search returned no results (HTTP 404)");
            }
        }

        if (candidates.Count == 0 && request.Page is null && !string.IsNullOrWhiteSpace(request.Author))
        {
            requestCount++;
            strategies.Add("author_fallback");
            try
            {
                var fallback = await GetByAuthorCoreAsync(request.Author, request.Region, true, cancellationToken);
                candidates.AddRange(fallback.Select(result => result with { LookupStrategy = "author_fallback" }));
                if (fallback.Count > 0)
                    warnings.Add("structured search returned no candidates; used native author lookup fallback");
            }
            catch (ProviderException exception) when (IsNotFound(exception))
            {
                warnings.Add("native author lookup fallback returned no results (HTTP 404)");
            }
        }

        var unique = candidates.GroupBy(item => item.ProviderRecordId ?? $"{item.Title}|{string.Join(',', item.Authors)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).Take(request.LimitPerProvider * 2).ToList();
        if (!string.IsNullOrWhiteSpace(request.Isbn))
            warnings.Add("Libex live search does not document an ISBN filter; ISBN was retained for local ranking only");
        if (!string.IsNullOrWhiteSpace(request.Series))
            warnings.Add("Libex live search does not document a series filter; series was retained for local ranking only");
        return new ProviderSearchResponse { Candidates = unique, RequestCount = requestCount, Warnings = warnings, LookupStrategies = strategies };
    }

    public async Task<SearchResult> GetAsync(string id, string? region, bool includeRaw, CancellationToken cancellationToken)
    {
        id = LibexAsin.Normalize(id);
        if (!includeRaw)
        {
            var book = await KiotaInvoker.InvokeAsync(Id, () => client.Book[id].GetAsync(options =>
            {
                options.QueryParameters.Region = region ?? config.Region;
            }, cancellationToken), cancellationToken);
            return book is null || mapper.Map(book) is not { } mapped ? throw new ProviderException(Id, "invalid_response", "Libex book response has no title") : mapped;
        }
        return await GetRawAsync(id, region, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchResult>> GetByAuthorAsync(
        string author,
        string? region,
        bool includeRaw,
        CancellationToken cancellationToken)
    {
        var results = await GetByAuthorCoreAsync(author, region, includeRaw, cancellationToken);
        return results.Select(result => result with { LookupStrategy = "author_books" }).ToList();
    }

    private async Task<SearchResult> GetRawAsync(string id, string? region, CancellationToken cancellationToken)
    {
        var uri = ProviderTransport.BuildUri(config.BaseUrl, $"book/{Uri.EscapeDataString(id)}",
        [
            new("region", region ?? config.Region)
        ]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = ParseDocument(response.Content);
        var result = ParseBook(document.RootElement, true);
        return result ?? throw new ProviderException(Id, "invalid_response", "Libex book response has no title");
    }

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            _ = await KiotaInvoker.InvokeAsync(Id, () => client.Health.GetAsync(cancellationToken: cancellationToken), cancellationToken);
            return new ProviderTestResult(Id, "ok", timer.ElapsedMilliseconds, "Libex health endpoint responded");
        }
        catch (Exception exception) when (exception is ProviderException or JsonException)
        {
            return new ProviderTestResult(Id, "error", timer.ElapsedMilliseconds, exception.Message);
        }
    }

    private async Task<ProviderSearchResponse> SearchTypedAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Sku))
        {
            var results = await GetBySkuAsync(request.Sku, false, cancellationToken);
            return new ProviderSearchResponse { Candidates = results, RequestCount = 1, LookupStrategies = ["sku"] };
        }
        if (!string.IsNullOrWhiteSpace(request.Asin))
        {
            var result = (await GetAsync(request.Asin, request.Region, false, cancellationToken)) with { LookupStrategy = "asin" };
            return new ProviderSearchResponse { Candidates = [result], RequestCount = 1, LookupStrategies = ["asin"] };
        }

        var books = new List<AudiobookMeta.Tool.Generated.Libex.Models.BookResponse>();
        var candidates = new List<SearchResult>();
        var warnings = new List<string>();
        var strategies = new List<string>();
        var requestCount = 0;
        var text = request.Query ?? request.Title;
        if (request.Page is null && !request.Exact && !string.IsNullOrWhiteSpace(text))
        {
            requestCount++;
            strategies.Add("quick_search");
            try
            {
                var quick = await KiotaInvoker.InvokeAsync(Id, () => client.QuickSearch.GetAsync(options =>
                {
                    options.QueryParameters.Keywords = text;
                    options.QueryParameters.Region = request.Region ?? config.Region;
                }, cancellationToken), cancellationToken);
                books.AddRange(quick ?? []);
                candidates.AddRange(books.Select(mapper.Map).Where(result => result is not null).Select(result => result! with { LookupStrategy = "quick_search" }));
            }
            catch (ProviderException exception) when (IsNotFound(exception))
            {
                warnings.Add("quick search returned no results (HTTP 404)");
            }
        }
        if (requestCount == 0 || books.Count < request.LimitPerProvider || HasStructuredHints(request))
        {
            requestCount++;
            strategies.Add("structured_search");
            try
            {
                var found = await KiotaInvoker.InvokeAsync(Id, () => client.Search.GetAsync(options =>
                {
                    options.QueryParameters.Title = request.Title;
                    options.QueryParameters.Author = request.Author;
                    options.QueryParameters.Narrator = request.Narrator;
                    options.QueryParameters.Query = request.Query;
                    options.QueryParameters.Keywords = request.Query;
                    options.QueryParameters.Limit = Math.Min(request.LimitPerProvider, LibexDefaults.MaximumSearchLimit);
                    options.QueryParameters.Page = request.Page ?? LibexDefaults.DefaultSearchPage;
                    options.QueryParameters.Cache = false;
                    options.QueryParameters.Region = request.Region ?? config.Region;
                }, cancellationToken), cancellationToken);
                books.AddRange(found ?? []);
                candidates.AddRange((found ?? []).Select(mapper.Map).Where(result => result is not null).Select(result => result! with { LookupStrategy = "structured_search" }));
            }
            catch (ProviderException exception) when (IsNotFound(exception))
            {
                warnings.Add("structured search returned no results (HTTP 404)");
            }
        }

        if (candidates.Count == 0 && request.Page is null && !string.IsNullOrWhiteSpace(request.Author))
        {
            requestCount++;
            strategies.Add("author_fallback");
            try
            {
                var fallback = await GetByAuthorCoreAsync(request.Author, request.Region, false, cancellationToken);
                candidates.AddRange(fallback.Select(result => result with { LookupStrategy = "author_fallback" }));
                if (fallback.Count > 0)
                    warnings.Add("structured search returned no candidates; used native author lookup fallback");
            }
            catch (ProviderException exception) when (IsNotFound(exception))
            {
                warnings.Add("native author lookup fallback returned no results (HTTP 404)");
            }
        }

        candidates = candidates.GroupBy(result => result.ProviderRecordId ?? $"{result.Title}|{string.Join(',', result.Authors)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).Take(request.LimitPerProvider * 2).ToList();
        if (!string.IsNullOrWhiteSpace(request.Isbn)) warnings.Add("Libex live search does not document an ISBN filter; ISBN was retained for local ranking only");
        if (!string.IsNullOrWhiteSpace(request.Series)) warnings.Add("Libex live search does not document a series filter; series was retained for local ranking only");
        return new ProviderSearchResponse { Candidates = candidates, RequestCount = requestCount, Warnings = warnings, LookupStrategies = strategies };
    }

    private async Task<List<SearchResult>> GetBySkuAsync(string sku, bool includeRaw, CancellationToken cancellationToken)
    {
        var uri = ProviderTransport.BuildUri(config.BaseUrl, $"book/sku/{Uri.EscapeDataString(sku.Trim())}", []);
        try
        {
            var response = await transport.GetAsync(config, uri, cancellationToken);
            return ParseBooks(response.Content, includeRaw).Select(result => result with { LookupStrategy = "sku" }).ToList();
        }
        catch (ProviderException exception) when (IsNotFound(exception))
        {
            return [];
        }
    }

    private async Task<List<SearchResult>> GetByAuthorCoreAsync(
        string author,
        string? region,
        bool includeRaw,
        CancellationToken cancellationToken)
    {
        var uri = ProviderTransport.BuildUri(config.BaseUrl, "author/books",
        [
            new("name", author),
            new("region", region ?? config.Region)
        ]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        return ParseBooks(response.Content, includeRaw);
    }

    private List<SearchResult> ParseBooks(byte[] content, bool includeRaw)
    {
        using var document = ParseDocument(content);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ProviderException(Id, "invalid_response", "Libex search response is not an array");
        return document.RootElement.EnumerateArray().Select(item => ParseBook(item, includeRaw)).Where(item => item is not null).Select(item => item!).ToList();
    }

    private SearchResult? ParseBook(JsonElement item, bool includeRaw)
    {
        var title = JsonFields.String(item, "title");
        if (string.IsNullOrWhiteSpace(title))
            return null;
        var asin = JsonFields.String(item, "asin");
        var releaseDate = JsonFields.String(item, "releaseDate");
        return new SearchResult
        {
            Provider = Id,
            ProviderType = AdapterType,
            ProviderRegion = config.Region,
            ProviderRecordId = asin,
            Title = title,
            Subtitle = JsonFields.String(item, "subtitle"),
            Authors = JsonFields.Strings(item, "authors"),
            Narrators = JsonFields.Strings(item, "narrators"),
            Series = JsonFields.Series(item),
            Identifiers = JsonFields.Identifiers(asin, JsonFields.String(item, "isbn"), Other(item)),
            Publisher = JsonFields.String(item, "publisher"),
            PublishedYear = releaseDate?.Length >= 4 ? releaseDate[..4] : null,
            ReleaseDate = releaseDate,
            Language = JsonFields.String(item, "language"),
            DurationSeconds = JsonFields.Integer(item, "lengthMinutes") is { } minutes ? minutes * 60 : null,
            Rating = JsonFields.Number(item, "rating"),
            Format = JsonFields.String(item, "bookFormat"),
            Regions = Regions(item),
            IsAvailable = JsonFields.Boolean(item, "isAvailable"),
            IsBuyable = JsonFields.Boolean(item, "isBuyable"),
            IsListenable = JsonFields.Boolean(item, "isListenable"),
            IsVirtualVoice = JsonFields.Boolean(item, "isVvab"),
            Genres = JsonFields.Strings(item, "genres"),
            CoverUrl = JsonFields.String(item, "imageUrl"),
            Description = JsonFields.String(item, "description", "summary"),
            SourceUrl = JsonFields.String(item, "link"),
            Raw = includeRaw ? item.Clone() : null
        };
    }

    private static Dictionary<string, object> Other(JsonElement item)
    {
        var result = new Dictionary<string, object>();
        foreach (var name in new[] { "sku", "skuGroup" })
            if (JsonFields.String(item, name) is { } value)
                result[name] = value;
        return result;
    }

    private static List<string> Regions(JsonElement item)
    {
        var regions = JsonFields.Strings(item, "regions");
        if (JsonFields.String(item, "region") is { } region)
        {
            regions.RemoveAll(value => value.Equals(region, StringComparison.OrdinalIgnoreCase));
            regions.Insert(0, region);
        }
        return regions;
    }

    private JsonDocument ParseDocument(byte[] content)
    {
        try { return JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException exception) { throw new ProviderException(Id, "invalid_response", "Libex returned invalid JSON", inner: exception); }
    }

    private static bool HasStructuredHints(SearchRequest request)
        => new[] { request.Author, request.Narrator, request.Title }.Any(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsNotFound(ProviderException exception)
        => exception.StatusCode == (int)HttpStatusCode.NotFound;
}
