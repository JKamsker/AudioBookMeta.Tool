using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace AudiobookMeta.Tool.Providers.Lismio;

internal static partial class LismioPageParser
{
    internal static LismioPage ParsePage(string html, Uri pageUrl, int page, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var document = new HtmlParser().ParseDocument(html);
        var items = new List<LismioSummary>();
        foreach (var card in document.QuerySelectorAll(".ab-grid .appear-in[data-template]"))
        {
            var anchor = card.QuerySelector("a[href*='/audiobook/']");
            var url = Absolute(anchor?.GetAttribute("href"), pageUrl);
            if (url is null || !TryAudiobookId(url, out var id)) continue;

            var title = Clean(card.QuerySelector(".line-clamp-2")?.TextContent);
            if (title.Length == 0)
            {
                title = anchor?.GetAttribute("aria-label")
                    ?.Replace("Audiobook ", string.Empty, StringComparison.Ordinal) ?? $"Audiobook {id}";
            }
            items.Add(new(
                id,
                title,
                NullIfEmpty(Clean(card.QuerySelector(".line-clamp-1")?.TextContent)),
                url,
                Absolute(card.QuerySelector("img")?.GetAttribute("src"), pageUrl)));
            if (items.Count >= limit) break;
        }

        if (items.Count == 0 && document.QuerySelector(".ab-grid") is null)
        {
            throw new InvalidDataException("Lismio's page format was not recognized: no audiobook cards were found.");
        }

        var total = document.QuerySelectorAll("nav[aria-label='Pagination Navigation'] p span.font-medium")
            .Select(element => ParseInteger(Clean(element.TextContent)))
            .LastOrDefault(value => value.HasValue) ?? items.Count;
        return new(items, page, items.Count, total, document.QuerySelector("a[rel='next']") is not null);
    }

    internal static LismioAudiobook ParseAudiobook(string html, Uri pageUrl, long expectedId)
    {
        var document = new HtmlParser().ParseDocument(html);
        var schema = FindAudiobookSchema(document);
        var title = JsonString(schema, "name") ?? Clean(document.QuerySelector("h1")?.TextContent);
        if (title.Length == 0) throw new InvalidDataException("Lismio's detail page has no audiobook title.");

        var canonical = Absolute(JsonString(schema, "url"), pageUrl) ?? pageUrl;
        var id = TryAudiobookId(canonical, out var parsedId) ? parsedId : expectedId;
        var creators = SchemaNames(schema, "author");
        var contributors = ParseContributors(document, canonical);
        var authors = contributors.Where(person => ContainsRole(person.Role, "autor", "author"))
            .Select(person => person.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var narrators = contributors.Where(person => ContainsRole(person.Role, "sprecher", "narrator"))
            .Select(person => person.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var versions = ParseVersions(document, canonical);
        var versionText = Clean(document.All.FirstOrDefault(element => element.Attributes.Any(attribute =>
            attribute.Name.Equals("wire:key", StringComparison.OrdinalIgnoreCase)
            && attribute.Value.StartsWith("version-", StringComparison.Ordinal)))?.TextContent);
        var ean = EanRegex().Match(versionText) is { Success: true } eanMatch ? eanMatch.Value : null;
        var releaseText = Clean(document.QuerySelector("[data-testid='audiobook-release-date']")?.TextContent);
        var releaseDate = DateRegex().Match(releaseText) is { Success: true } dateMatch ? dateMatch.Value : null;
        var shops = SchemaOffers(schema, canonical).Concat(versions.SelectMany(item => item.Shops))
            .DistinctBy(link => link.Url).OrderBy(link => link.Provider, StringComparer.Ordinal).ToArray();

        return new(
            id,
            title,
            creators,
            authors,
            narrators,
            contributors,
            NullIfEmpty(Clean(document.QuerySelector("[data-testid='audiobook-series-link']")?.TextContent)),
            NullIfEmpty(Clean(document.QuerySelector("#tags a[href*='/label/']")?.TextContent)),
            NullIfEmpty(document.QuerySelector("meta[name='description']")?.GetAttribute("content")),
            releaseDate,
            ParseYear(releaseDate),
            ParseDuration(versionText),
            ean,
            ParseAbridged(versionText) ?? JsonBoolean(schema, "abridged"),
            Absolute(document.QuerySelector("meta[name='image']")?.GetAttribute("content"), canonical)
                ?? Absolute(document.QuerySelector("meta[property='og:image']")?.GetAttribute("content"), canonical),
            canonical,
            Absolute(document.QuerySelector("[data-testid='audiobook-shortlink'] a[href]")?.GetAttribute("href"), canonical),
            shops,
            NullIfEmpty(Clean(document.QuerySelector("[data-testid='audiobook-liner-notes']")?.TextContent)),
            versions,
            ParseCollections(document, canonical));
    }

    private static LismioVersion[] ParseVersions(IDocument document, Uri pageUrl) => document.All
        .Where(element => element.Attributes.Any(attribute =>
            attribute.Name.Equals("wire:key", StringComparison.OrdinalIgnoreCase)
            && attribute.Value.StartsWith("version-", StringComparison.Ordinal)))
        .Select(element =>
        {
            var key = element.Attributes.First(attribute =>
                attribute.Name.Equals("wire:key", StringComparison.OrdinalIgnoreCase)).Value;
            var text = Clean(element.TextContent);
            var ean = EanRegex().Match(text) is { Success: true } match ? match.Value : null;
            var links = element.QuerySelectorAll("a[href]")
                .Select(link => Absolute(link.GetAttribute("href"), pageUrl))
                .WhereNotNull()
                .Where(url => !url.Host.Equals(pageUrl.Host, StringComparison.OrdinalIgnoreCase))
                .Select(url => new LismioShopLink(LismioShopClassifier.FromUrl(url), url))
                .DistinctBy(link => link.Url)
                .ToArray();
            return new LismioVersion(
                key["version-".Length..].Trim(), ean, ParseDuration(text), ParseAbridged(text), links);
        })
        .ToArray();

    private static LismioCollection[] ParseCollections(IDocument document, Uri pageUrl) => document
        .QuerySelectorAll("a[href*='/collection/']")
        .Select(link =>
        {
            var url = Absolute(link.GetAttribute("href"), pageUrl);
            if (url is null) return null;
            var match = CollectionIdRegex().Match(url.AbsolutePath);
            var name = Clean(link.QuerySelector(".line-clamp-2")?.TextContent);
            if (name.Length == 0) name = Clean(link.TextContent);
            if (name.Length == 0) name = Clean(link.GetAttribute("aria-label"));
            return new LismioCollection(name, match.Success ? match.Groups[1].Value : null, url);
        })
        .WhereNotNull()
        .DistinctBy(collection => collection.Url)
        .ToArray();

    private static JsonElement? FindAudiobookSchema(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                if (IsAudiobook(json.RootElement)) return json.RootElement.Clone();
                if (!json.RootElement.TryGetProperty("@graph", out var graph)
                    || graph.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in graph.EnumerateArray())
                {
                    if (IsAudiobook(item)) return item.Clone();
                }
            }
            catch (JsonException)
            {
                // Ignore unrelated malformed structured data and retain HTML fallbacks.
            }
        }
        return null;
    }

    private static bool IsAudiobook(JsonElement value) =>
        JsonString(value, "@type")?.Equals("Audiobook", StringComparison.OrdinalIgnoreCase) is true;

    private static string[] SchemaNames(JsonElement? schema, string property)
    {
        if (schema is not { } root || !root.TryGetProperty(property, out var value)) return [];
        return value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Select(NameFromSchema).WhereNotNull().ToArray(),
            _ => NameFromSchema(value) is { } name ? [name] : [],
        };
    }

    private static string? NameFromSchema(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Object => JsonString(value, "name"),
        _ => null,
    };

    private static LismioShopLink[] SchemaOffers(JsonElement? schema, Uri pageUrl)
    {
        if (schema is not { } root || !root.TryGetProperty("offers", out var offers)) return [];
        var values = offers.ValueKind == JsonValueKind.Array ? offers.EnumerateArray().ToArray() : [offers];
        return values.Select(offer => Absolute(JsonString(offer, "url"), pageUrl))
            .WhereNotNull()
            .Select(url => new LismioShopLink(LismioShopClassifier.FromUrl(url), url))
            .ToArray();
    }

    private static LismioContributor[] ParseContributors(IDocument document, Uri pageUrl) => document
        .QuerySelectorAll("a[aria-label^='View artist']")
        .Select(link => new LismioContributor(
            link.GetAttribute("aria-label")?.Replace("View artist ", string.Empty, StringComparison.Ordinal) ?? "Unknown",
            NullIfEmpty(Clean(link.QuerySelector(".line-clamp-1")?.TextContent)) ?? "Contributor",
            Absolute(link.GetAttribute("href"), pageUrl)))
        .Distinct()
        .ToArray();

    private static bool ContainsRole(string role, params string[] values) =>
        values.Any(value => role.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static string? JsonString(JsonElement? value, string property) => value is { } element
        && element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var child)
        && child.ValueKind == JsonValueKind.String ? child.GetString() : null;

    private static bool? JsonBoolean(JsonElement? value, string property) => value is { } element
        && element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var child)
        && child.ValueKind is JsonValueKind.True or JsonValueKind.False ? child.GetBoolean() : null;

    private static int? ParseDuration(string value)
    {
        var matches = DurationRegex().Matches(value);
        if (matches.Count == 0) return null;
        var minutes = 0;
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var amount)) continue;
            var unit = match.Groups[2].Value;
            minutes += unit.StartsWith("Stund", StringComparison.OrdinalIgnoreCase)
                || unit.StartsWith("Hour", StringComparison.OrdinalIgnoreCase) ? amount * 60 : amount;
        }
        return minutes;
    }

    private static bool? ParseAbridged(string value) =>
        value.Contains("Ungekürzt", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Unabridged", StringComparison.OrdinalIgnoreCase) ? false
        : value.Contains("Gekürzt", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Abridged", StringComparison.OrdinalIgnoreCase) ? true : null;

    private static int? ParseYear(string? date)
    {
        if (date is null) return null;
        var part = date.Split('.').Last();
        if (!int.TryParse(part, CultureInfo.InvariantCulture, out var year)) return null;
        return year switch { <= 29 => 2000 + year, < 100 => 1900 + year, _ => year };
    }

    internal static bool TryAudiobookId(Uri url, out long id)
    {
        var match = AudiobookIdRegex().Match(url.AbsolutePath);
        return long.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out id);
    }

    private static int? ParseInteger(string value) => int.TryParse(
        value.Replace(".", string.Empty, StringComparison.Ordinal),
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var number) ? number : null;

    private static Uri? Absolute(string? value, Uri pageUrl) =>
        Uri.TryCreate(value, UriKind.Absolute, out var absolute) ? absolute
        : Uri.TryCreate(pageUrl, value, out var relative) ? relative : null;

    private static string Clean(string? value) => WhitespaceRegex().Replace(value ?? string.Empty, " ").Trim();
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"/audiobook/(\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudiobookIdRegex();
    [GeneratedRegex(@"/collection/(\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CollectionIdRegex();
    [GeneratedRegex(@"\b\d{13}\b", RegexOptions.CultureInvariant)]
    private static partial Regex EanRegex();
    [GeneratedRegex(@"\b\d{1,2}\.\d{1,2}\.\d{2,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();
    [GeneratedRegex(@"\b(\d+)\s*(Minuten?|Minutes?|Stunden?|Hours?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

file static class LismioEnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class =>
        source.Where(item => item is not null).Select(item => item!);
}
