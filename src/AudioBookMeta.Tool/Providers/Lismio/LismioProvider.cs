using System.Globalization;
using System.Text;
using System.Text.Json;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.Lismio;

public sealed class LismioProvider(ProviderConfig config, ProviderTransport transport) : IMetadataProvider
{
    private const int DetailConcurrency = 4;
    private readonly LismioModelMapper mapper = new(config.Id);

    public string Id => config.Id;
    public string AdapterType => "lismio";
    public ProviderCapabilities Capabilities { get; } = CapabilityCatalog.Create(
        config, "search", "free_text_query", "region_filter", "get_by_id", "native_pagination", "shop_links");

    public async Task<ProviderSearchResponse> SearchAsync(
        SearchRequest request,
        bool includeRaw,
        CancellationToken cancellationToken)
    {
        var query = SearchText(request);
        var locale = Locale(request.Region ?? config.Region);
        var page = (request.Page ?? 0) + 1;
        var uri = SearchUri(query, locale, page);
        var response = await transport.GetHtmlAsync(config, uri, cancellationToken);
        var html = Encoding.UTF8.GetString(response.Content);
        var summaries = LismioPageParser.ParsePage(html, response.Uri, page, request.LimitPerProvider);
        var warnings = new string?[summaries.Items.Count];
        var results = new SearchResult[summaries.Items.Count];
        using var gate = new SemaphoreSlim(DetailConcurrency);
        var tasks = summaries.Items.Select((summary, index) => HydrateAsync(
            summary, index, locale, includeRaw, results, warnings, gate, cancellationToken));
        await Task.WhenAll(tasks);
        return new ProviderSearchResponse
        {
            Candidates = [.. results],
            Warnings = warnings.Where(message => message is not null).Select(message => message!).ToList(),
            RequestCount = 1 + summaries.Items.Count
        };
    }

    public async Task<SearchResult> GetAsync(
        string id,
        string? region,
        bool includeRaw,
        CancellationToken cancellationToken)
    {
        var recordId = RecordId(id);
        var locale = Locale(region ?? config.Region);
        var pageUri = DetailUri(recordId, locale);
        var response = await transport.GetHtmlAsync(config, pageUri, cancellationToken);
        try
        {
            var html = Encoding.UTF8.GetString(response.Content);
            return mapper.Map(
                LismioPageParser.ParseAudiobook(html, response.Uri, recordId),
                locale,
                Raw(includeRaw, html));
        }
        catch (InvalidDataException exception)
        {
            throw new ProviderException(config.Id, "invalid_response", exception.Message, inner: exception);
        }
    }

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var locale = Locale(config.Region);
        var uri = SearchUri("a", locale, 1);
        var response = await transport.GetHtmlAsync(config, uri, cancellationToken);
        try
        {
            _ = LismioPageParser.ParsePage(Encoding.UTF8.GetString(response.Content), response.Uri, 1, 1);
        }
        catch (InvalidDataException exception)
        {
            throw new ProviderException(config.Id, "invalid_response", exception.Message, inner: exception);
        }
        return new(config.Id, "ok", timer.ElapsedMilliseconds, "Lismio catalogue search is reachable and parseable");
    }

    private async Task HydrateAsync(
        LismioSummary summary,
        int index,
        string locale,
        bool includeRaw,
        SearchResult[] results,
        string?[] warnings,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var response = await transport.GetHtmlAsync(config, DetailUri(summary.Id, locale), cancellationToken);
            var html = Encoding.UTF8.GetString(response.Content);
            var book = LismioPageParser.ParseAudiobook(html, response.Uri, summary.Id);
            results[index] = mapper.Map(book, locale, Raw(includeRaw, html));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ProviderException or InvalidDataException)
        {
            var message = $"detail hydration failed for Lismio audiobook {summary.Id}: {exception.Message}";
            warnings[index] = message;
            results[index] = mapper.MapSummary(summary, locale, message);
        }
        finally
        {
            gate.Release();
        }
    }

    private Uri SearchUri(string query, string locale, int page) => ProviderTransport.BuildUri(
        config.BaseUrl,
        $"{locale}/search",
        StaticQueryParameters().Concat(
        [
            new("keywords", query),
            new("type", "audiobooks"),
            new("page", page.ToString(CultureInfo.InvariantCulture))
        ]));

    private Uri DetailUri(long id, string locale) => ProviderTransport.BuildUri(
        config.BaseUrl,
        $"{locale}/audiobook/{id}",
        StaticQueryParameters());

    private IEnumerable<KeyValuePair<string, string?>> StaticQueryParameters() =>
        config.QueryParams.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value));

    private static JsonElement? Raw(bool includeRaw, string html) =>
        includeRaw ? JsonSerializer.SerializeToElement(new { detail_html = html }) : null;

    private static string SearchText(SearchRequest request)
    {
        var values = new[]
        {
            request.Query, request.Title, request.Author, request.Narrator, request.Series, request.Isbn, request.Asin
        };
        return string.Join(' ', values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
    }

    private static string Locale(string? value)
    {
        var locale = string.IsNullOrWhiteSpace(value) ? "de" : value.Trim().Trim('/');
        if (locale.Length == 0 || locale.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new AudiobookMetaException(
                $"Invalid Lismio locale '{value}'.",
                ExitCodes.Usage,
                "Use a locale such as 'de' in the provider's region setting or --region.");
        }
        return locale;
    }

    private static long RecordId(string reference)
    {
        if (long.TryParse(reference, CultureInfo.InvariantCulture, out var directId) && directId > 0) return directId;
        if (Uri.TryCreate(reference, UriKind.Absolute, out var uri)
            && LismioPageParser.TryAudiobookId(uri, out var parsedId) && parsedId > 0) return parsedId;
        throw new AudiobookMetaException(
            $"Invalid Lismio audiobook reference '{reference}'.",
            ExitCodes.Usage,
            "Use a positive numeric Lismio audiobook ID or audiobook URL.");
    }
}
