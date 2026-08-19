using System.Diagnostics;
using System.Net;
using System.Text.Json;
using BookMeta.Common;
using BookMeta.Configuration;
using BookMeta.Model;

namespace BookMeta.Providers.AudioSilo;

public sealed class AudioSiloProvider(ProviderConfig config, ProviderTransport transport, KiotaClientFactory kiota) : IMetadataProvider
{
    private readonly BookMeta.Generated.AudioSilo.AudioSiloApiClient client = kiota.CreateAudioSilo(config);
    private readonly AudioSiloModelMapper mapper = new(config.Id);
    public string Id => config.Id;
    public string AdapterType => "audiosilo";
    public ProviderCapabilities Capabilities { get; } = CapabilityCatalog.Create(config,
        "search", "free_text_query", "title_filter", "author_filter", "series_filter", "isbn_filter", "asin_filter",
        "quick_search", "get_by_id", "chapters", "author_search", "series_search", "native_pagination", "health");

    public async Task<ProviderSearchResponse> SearchAsync(SearchRequest request, bool includeRaw, CancellationToken cancellationToken)
    {
        if (!includeRaw)
            return await SearchTypedAsync(request, cancellationToken);
        return await SearchRawAsync(request, cancellationToken);
    }

    private async Task<ProviderSearchResponse> SearchRawAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Asin) || !string.IsNullOrWhiteSpace(request.Isbn))
            return await IdentifierSearchRawAsync(request, cancellationToken);

        var text = request.Query ?? string.Join(' ', new[] { request.Title, request.Author, request.Series, request.Narrator }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var uri = ProviderTransport.BuildUri(config.BaseUrl, "api/v1/search",
        [
            new("q", text), new("limit", Math.Min(request.LimitPerProvider, 100).ToString())
        ]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = ParseDocument(response.Content);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            throw new ProviderException(Id, "invalid_response", "AudioSilo search response is missing a results array");
        var candidates = results.EnumerateArray().Where(item => JsonFields.String(item, "kind") == "work")
            .Select(item => ParseWorkCard(item, true)).Where(item => item is not null).Select(item => item!).ToList();
        return new ProviderSearchResponse { Candidates = candidates, RequestCount = 1 };
    }

    public async Task<SearchResult> GetAsync(string id, bool includeRaw, CancellationToken cancellationToken)
    {
        if (!includeRaw)
            return await GetTypedAsync(id, cancellationToken);
        if (id.StartsWith("work/", StringComparison.OrdinalIgnoreCase))
            return await GetWorkRawAsync(id[5..], null, true, cancellationToken);
        if (id.StartsWith("isbn/", StringComparison.OrdinalIgnoreCase))
            return await LookupRawAsync("isbn", id[5..], true, cancellationToken);
        if (id.StartsWith("asin/", StringComparison.OrdinalIgnoreCase))
            return await LookupRawAsync("asin", id[5..], true, cancellationToken);
        if (id.Length == 10 && id.All(char.IsLetterOrDigit))
            return await LookupRawAsync("asin", id, true, cancellationToken);
        return await GetWorkRawAsync(id, null, true, cancellationToken);
    }

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            _ = await KiotaInvoker.InvokeAsync(Id, () => client.Healthz.GetAsync(cancellationToken: cancellationToken), cancellationToken);
            return new ProviderTestResult(Id, "ok", timer.ElapsedMilliseconds, "AudioSilo health endpoint responded");
        }
        catch (Exception exception) when (exception is ProviderException or JsonException)
        {
            return new ProviderTestResult(Id, "error", timer.ElapsedMilliseconds, exception.Message);
        }
    }

    private async Task<ProviderSearchResponse> SearchTypedAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Asin) || !string.IsNullOrWhiteSpace(request.Isbn))
        {
            try
            {
                var result = await LookupTypedAsync(request.Asin is not null ? "asin" : "isbn", request.Asin ?? request.Isbn!, cancellationToken);
                return new ProviderSearchResponse { Candidates = [result], RequestCount = 2 };
            }
            catch (ProviderException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return new ProviderSearchResponse { RequestCount = 1 };
            }
        }

        var text = request.Query ?? string.Join(' ', new[] { request.Title, request.Author, request.Series, request.Narrator }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var response = await KiotaInvoker.InvokeAsync(Id, () => client.Api.V1.Search.GetAsync(options =>
        {
            options.QueryParameters.Q = text;
            options.QueryParameters.Limit = Math.Min(request.LimitPerProvider, 100);
        }, cancellationToken), cancellationToken);
        var candidates = response?.Results?.Select(result => result.WorkResult).Where(work => work is not null)
            .Select(work => mapper.MapCard(work!)).Where(result => result is not null).Select(result => result!).ToList() ?? [];
        return new ProviderSearchResponse { Candidates = candidates, RequestCount = 1 };
    }

    private async Task<SearchResult> GetTypedAsync(string id, CancellationToken cancellationToken)
    {
        if (id.StartsWith("work/", StringComparison.OrdinalIgnoreCase))
            return await GetWorkTypedAsync(id[5..], null, cancellationToken);
        if (id.StartsWith("isbn/", StringComparison.OrdinalIgnoreCase))
            return await LookupTypedAsync("isbn", id[5..], cancellationToken);
        if (id.StartsWith("asin/", StringComparison.OrdinalIgnoreCase))
            return await LookupTypedAsync("asin", id[5..], cancellationToken);
        if (id.Length == 10 && id.All(char.IsLetterOrDigit))
            return await LookupTypedAsync("asin", id, cancellationToken);
        return await GetWorkTypedAsync(id, null, cancellationToken);
    }

    private async Task<SearchResult> LookupTypedAsync(string kind, string value, CancellationToken cancellationToken)
    {
        var lookup = await KiotaInvoker.InvokeAsync(Id, () => client.Api.V1.Lookup.GetAsync(options =>
        {
            if (kind == "asin") options.QueryParameters.Asin = value;
            else options.QueryParameters.Isbn = value.Replace("-", string.Empty, StringComparison.Ordinal);
        }, cancellationToken), cancellationToken);
        var workId = lookup?.Work?.Id ?? throw new ProviderException(Id, "invalid_response", "AudioSilo lookup response has no work id");
        return await GetWorkTypedAsync(workId, lookup.RecordingId, cancellationToken);
    }

    private async Task<SearchResult> GetWorkTypedAsync(string workId, string? recordingId, CancellationToken cancellationToken)
    {
        var work = await KiotaInvoker.InvokeAsync(Id, () => client.Api.V1.Works[workId].GetAsync(cancellationToken: cancellationToken), cancellationToken);
        return work is null || mapper.MapDetail(work, recordingId) is not { } result
            ? throw new ProviderException(Id, "invalid_response", "AudioSilo work response has no title") : result;
    }

    private async Task<ProviderSearchResponse> IdentifierSearchRawAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await LookupRawAsync(request.Asin is not null ? "asin" : "isbn", request.Asin ?? request.Isbn!, true, cancellationToken);
            return new ProviderSearchResponse { Candidates = [result], RequestCount = 2 };
        }
        catch (ProviderException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return new ProviderSearchResponse { RequestCount = 1 };
        }
    }

    private async Task<SearchResult> LookupRawAsync(string kind, string value, bool includeRaw, CancellationToken cancellationToken)
    {
        var uri = ProviderTransport.BuildUri(config.BaseUrl, "api/v1/lookup", [new(kind, value.Replace("-", string.Empty, StringComparison.Ordinal))]);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = ParseDocument(response.Content);
        if (!document.RootElement.TryGetProperty("work", out var work))
            throw new ProviderException(Id, "invalid_response", "AudioSilo lookup response has no work");
        var workId = JsonFields.String(work, "id") ?? throw new ProviderException(Id, "invalid_response", "AudioSilo lookup work has no id");
        return await GetWorkRawAsync(workId, JsonFields.String(document.RootElement, "recording_id"), includeRaw, cancellationToken);
    }

    private async Task<SearchResult> GetWorkRawAsync(string workId, string? recordingId, bool includeRaw, CancellationToken cancellationToken)
    {
        var uri = ProviderTransport.BuildUri(config.BaseUrl, $"api/v1/works/{Uri.EscapeDataString(workId)}", []);
        var response = await transport.GetAsync(config, uri, cancellationToken);
        using var document = ParseDocument(response.Content);
        var result = ParseWorkDetail(document.RootElement, recordingId, includeRaw);
        return result ?? throw new ProviderException(Id, "invalid_response", "AudioSilo work response has no title");
    }

    private SearchResult? ParseWorkCard(JsonElement item, bool includeRaw)
    {
        var title = JsonFields.String(item, "title");
        if (title is null)
            return null;
        return new SearchResult
        {
            Provider = Id, ProviderType = AdapterType, ProviderRecordId = $"work/{JsonFields.String(item, "id")}", Title = title,
            Authors = JsonFields.Strings(item, "authors"), Narrators = JsonFields.Strings(item, "narrators"), Series = JsonFields.Series(item),
            Identifiers = new Identifiers(), CoverUrl = JsonFields.String(item, "cover_url"), Raw = includeRaw ? item.Clone() : null
        };
    }

    private SearchResult? ParseWorkDetail(JsonElement item, string? recordingId, bool includeRaw)
    {
        var title = JsonFields.String(item, "title");
        if (title is null)
            return null;
        JsonElement? recording = null;
        if (item.TryGetProperty("recordings", out var recordings) && recordings.ValueKind == JsonValueKind.Array)
            recording = recordings.EnumerateArray().FirstOrDefault(value => recordingId is null || JsonFields.String(value, "id") == recordingId);
        var identifiers = recording is { ValueKind: JsonValueKind.Object } ? RecordingIdentifiers(recording.Value) : new Identifiers();
        return new SearchResult
        {
            Provider = Id, ProviderType = AdapterType, ProviderRecordId = recording is { ValueKind: JsonValueKind.Object }
                ? $"work/{JsonFields.String(item, "id")}/recording/{JsonFields.String(recording.Value, "id")}" : $"work/{JsonFields.String(item, "id")}",
            Title = title, Subtitle = JsonFields.String(item, "subtitle"), Authors = JsonFields.Strings(item, "authors"),
            Narrators = recording is { ValueKind: JsonValueKind.Object } ? JsonFields.Strings(recording.Value, "narrators") : [], Series = JsonFields.Series(item),
            Identifiers = identifiers, Publisher = recording is { ValueKind: JsonValueKind.Object } ? JsonFields.String(recording.Value, "publisher") : null,
            PublishedYear = JsonFields.String(item, "first_published"), ReleaseDate = recording is { ValueKind: JsonValueKind.Object } ? JsonFields.String(recording.Value, "release_date") : null,
            Language = JsonFields.String(item, "language"), DurationSeconds = recording is { ValueKind: JsonValueKind.Object } && JsonFields.Integer(recording.Value, "runtime_min") is { } minutes ? minutes * 60 : null,
            Genres = JsonFields.Strings(item, "genres"), CoverUrl = recording is { ValueKind: JsonValueKind.Object } ? JsonFields.String(recording.Value, "cover_url") : null,
            Description = JsonFields.String(item, "description"), Raw = includeRaw ? item.Clone() : null
        };
    }

    private static Identifiers RecordingIdentifiers(JsonElement recording)
    {
        var result = new Identifiers();
        if (recording.TryGetProperty("asin", out var asins) && asins.ValueKind == JsonValueKind.Array)
            foreach (var item in asins.EnumerateArray()) JsonFields.AddIdentifier(result.Asin, JsonFields.String(item, "asin"), 10, true);
        if (recording.TryGetProperty("isbn", out var isbns) && isbns.ValueKind == JsonValueKind.Array)
            foreach (var item in isbns.EnumerateArray())
            {
                var value = item.GetString();
                JsonFields.AddIdentifier(value?.Length == 10 ? result.Isbn10 : result.Isbn13, value, value?.Length ?? 0);
            }
        return result;
    }

    private JsonDocument ParseDocument(byte[] content)
    {
        try { return JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException exception) { throw new ProviderException(Id, "invalid_response", "AudioSilo returned invalid JSON", inner: exception); }
    }
}
