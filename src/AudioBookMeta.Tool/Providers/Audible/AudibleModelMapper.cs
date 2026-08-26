using System.Text.Json;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.Audible;

internal sealed class AudibleModelMapper(string providerId, string marketplace, string domain)
{
    internal SearchResult? Map(JsonElement item, bool includeRaw, string strategy)
    {
        var title = JsonFields.String(item, "title");
        var asin = JsonFields.String(item, "asin");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(asin))
            return null;

        var sku = JsonFields.String(item, "sku");
        var skuLite = JsonFields.String(item, "sku_lite");
        var other = new Dictionary<string, object>();
        if (sku is not null) other["sku"] = sku;
        if (skuLite is not null) other["skuGroup"] = skuLite;
        var publication = JsonFields.String(item, "publication_datetime", "release_date");
        return new SearchResult
        {
            Provider = providerId,
            ProviderType = "audible",
            ProviderRecordId = asin,
            LookupStrategy = strategy,
            ProviderRegion = marketplace,
            Title = title,
            Subtitle = JsonFields.String(item, "subtitle"),
            Authors = Names(item, "authors"),
            Narrators = Names(item, "narrators"),
            Series = Series(item),
            Identifiers = JsonFields.Identifiers(asin, null, other),
            IdentifierProvenance = [new("asin", asin, $"audible_catalog:{marketplace}")],
            Publisher = JsonFields.String(item, "publisher_name"),
            PublishedYear = publication is { Length: >= 4 } ? publication[..4] : null,
            ReleaseDate = publication,
            Language = JsonFields.String(item, "language"),
            DurationSeconds = JsonFields.Integer(item, "runtime_length_min") * 60,
            Rating = Rating(item),
            Format = JsonFields.String(item, "format_type", "content_type") ?? "audiobook",
            Regions = [marketplace],
            CoverUrl = Cover(item),
            Description = JsonFields.String(item, "publisher_summary", "merchandising_summary"),
            SourceUrl = $"https://www.audible.{domain}/pd/{asin}",
            Raw = includeRaw ? item.Clone() : null
        };
    }

    private static List<string> Names(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
            return [];
        return values.EnumerateArray().Select(value => JsonFields.String(value, "name"))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<SeriesEntry> Series(JsonElement item)
    {
        if (!item.TryGetProperty("series", out var values) || values.ValueKind != JsonValueKind.Array)
            return [];
        return values.EnumerateArray().Select(value => new SeriesEntry(
                JsonFields.String(value, "title") ?? string.Empty,
                JsonFields.String(value, "sequence")))
            .Where(series => series.Name.Length > 0).ToList();
    }

    private static double? Rating(JsonElement item)
    {
        if (!item.TryGetProperty("rating", out var rating)
            || !rating.TryGetProperty("overall_distribution", out var distribution))
            return null;
        return JsonFields.Number(distribution, "average_rating", "display_average_rating");
    }

    private static string? Cover(JsonElement item)
    {
        if (!item.TryGetProperty("product_images", out var images) || images.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var size in new[] { "500", "1000", "300", "200" })
            if (JsonFields.String(images, size) is { } url)
                return url;
        return images.EnumerateObject().Select(property => property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null)
            .FirstOrDefault(value => value is not null);
    }
}
