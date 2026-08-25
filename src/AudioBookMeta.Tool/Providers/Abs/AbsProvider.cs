using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.Abs;

public sealed class AbsProvider(ProviderConfig config, ProviderTransport transport) : IMetadataProvider
{
    public string Id => config.Id;
    public string AdapterType => "abs";
    public ProviderCapabilities Capabilities { get; } = CapabilityCatalog.Create(config, "search", "free_text_query", "title_filter", "author_filter");

    public async Task<ProviderSearchResponse> SearchAsync(SearchRequest request, bool includeRaw, CancellationToken cancellationToken)
    {
        var query = request.Query ?? request.Title ?? string.Join(' ', new[] { request.Author, request.Series, request.Narrator, request.Publisher, request.Sku }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("query", query), new("author", request.Author)
        };
        foreach (var (name, value) in config.QueryParams)
            parameters.Add(new(name, value));
        AddSupported(parameters, "narrator", request.Narrator);
        AddSupported(parameters, "series", request.Series);
        AddSupported(parameters, "isbn", request.Isbn);
        AddSupported(parameters, "sku", request.Sku);
        AddSupported(parameters, "publisher", request.Publisher);
        AddSupported(parameters, "language", request.Language);
        var uri = ProviderTransport.BuildUri(config.BaseUrl, config.AppendSearchPath ? "search" : null, parameters);
        TransportResponse response;
        try
        {
            response = await transport.GetAsync(config, uri, cancellationToken);
        }
        catch (ProviderException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound && config.CapabilityOverrides.GetValueOrDefault("not_found_is_empty") == CapabilityState.Supported)
        {
            return new ProviderSearchResponse { RequestCount = 1, Warnings = ["provider returned its configured 404-as-empty response"] };
        }
        var parsed = Parse(response.Content, includeRaw);
        return parsed with
        {
            Candidates = parsed.Candidates.Select(result => result with { LookupStrategy = "search" }).ToList(),
            LookupStrategies = ["search"]
        };
    }

    public Task<SearchResult> GetAsync(string id, string? region, bool includeRaw, CancellationToken cancellationToken)
        => throw new AudiobookMetaException($"Provider '{Id}' does not support get by ID.", ExitCodes.UnsupportedCapability, "Use 'dotnet audiobookmeta search' for generic ABS providers.");

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            _ = await SearchAsync(new SearchRequest { Query = "test", LimitPerProvider = 1 }, false, cancellationToken);
            return new ProviderTestResult(Id, "ok", timer.ElapsedMilliseconds, "ABS search contract accepted a minimal request");
        }
        catch (Exception exception) when (exception is ProviderException or JsonException)
        {
            return new ProviderTestResult(Id, "error", timer.ElapsedMilliseconds, exception.Message);
        }
    }

    private ProviderSearchResponse Parse(byte[] content, bool includeRaw)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException exception) { throw new ProviderException(Id, "invalid_response", "provider returned invalid JSON", inner: exception); }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("matches", out var matches) || matches.ValueKind != JsonValueKind.Array)
                throw new ProviderException(Id, "invalid_response", "provider response is missing an array named 'matches'");
            var candidates = matches.EnumerateArray().Take(1000).Select(item => ParseMatch(item, includeRaw)).Where(result => result is not null).Select(result => result!).ToList();
            return new ProviderSearchResponse { Candidates = candidates, RequestCount = 1 };
        }
    }

    private SearchResult? ParseMatch(JsonElement item, bool includeRaw)
    {
        var title = JsonFields.String(item, "title");
        if (string.IsNullOrWhiteSpace(title))
            return null;
        var published = JsonFields.String(item, "publishedYear", "published_year");
        return new SearchResult
        {
            Provider = Id,
            ProviderType = AdapterType,
            ProviderRecordId = JsonFields.String(item, "id", "bookId", "asin", "isbn"),
            Title = title,
            Subtitle = JsonFields.String(item, "subtitle"),
            Authors = JsonFields.Strings(item, "authors", "author"),
            Narrators = JsonFields.Strings(item, "narrators", "narrator", "lector"),
            Series = JsonFields.Series(item),
            Identifiers = JsonFields.Identifiers(JsonFields.String(item, "asin"), JsonFields.String(item, "isbn"), OtherIdentifiers(item)),
            Publisher = JsonFields.String(item, "publisher"),
            PublishedYear = published,
            ReleaseDate = JsonFields.String(item, "releaseDate", "release_date"),
            Language = JsonFields.String(item, "language"),
            DurationSeconds = JsonFields.Integer(item, "duration", "durationSeconds", "duration_seconds"),
            Genres = JsonFields.Strings(item, "genres"),
            Tags = JsonFields.Strings(item, "tags"),
            CoverUrl = JsonFields.String(item, "cover", "coverUrl"),
            Description = JsonFields.String(item, "description"),
            SourceUrl = JsonFields.String(item, "sourceUrl", "link"),
            Raw = includeRaw ? item.Clone() : null
        };
    }

    private void AddSupported(List<KeyValuePair<string, string?>> parameters, string name, string? value)
    {
        if (value is not null && Capabilities[name + "_filter"] == CapabilityState.Supported)
            parameters.Add(new(name, value));
    }

    private static Dictionary<string, object> OtherIdentifiers(JsonElement item)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        Add("sku", "sku", "ufid");
        Add("skuGroup", "skuGroup", "sku_group");
        Add("ean", "ean");
        return result;

        void Add(string target, params string[] names)
        {
            if (JsonFields.String(item, names) is { } value)
                result[target] = value;
        }
    }
}
