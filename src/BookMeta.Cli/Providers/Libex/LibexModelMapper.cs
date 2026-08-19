using BookMeta.Generated.Libex.Models;
using BookMeta.Model;

namespace BookMeta.Providers.Libex;

internal sealed class LibexModelMapper(string providerId)
{
    public SearchResult? Map(BookResponse item)
    {
        var title = PrimitiveString(item.Title);
        if (string.IsNullOrWhiteSpace(title))
            return null;
        var releaseDate = PrimitiveString(item.ReleaseDate);
        return new SearchResult
        {
            Provider = providerId,
            ProviderType = "libex",
            ProviderRecordId = item.Asin,
            Title = title,
            Subtitle = PrimitiveString(item.Subtitle),
            Authors = item.Authors?.Select(author => PrimitiveString(author.Name)).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList() ?? [],
            Narrators = item.Narrators?.Select(narrator => narrator.Name).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList() ?? [],
            Series = item.Series?.Select(series => new SeriesEntry(PrimitiveString(series.Name) ?? string.Empty, PrimitiveString(series.Position))).Where(series => series.Name.Length > 0).ToList() ?? [],
            Identifiers = JsonFields.Identifiers(item.Asin, PrimitiveString(item.Isbn), Other(item)),
            Publisher = PrimitiveString(item.Publisher),
            PublishedYear = releaseDate?.Length >= 4 ? releaseDate[..4] : null,
            ReleaseDate = releaseDate,
            Language = PrimitiveString(item.Language),
            DurationSeconds = PrimitiveInteger(item.LengthMinutes) is { } minutes ? minutes * 60 : null,
            Genres = item.Genres?.Select(genre => PrimitiveString(genre.Name)).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList() ?? [],
            CoverUrl = PrimitiveString(item.ImageUrl),
            Description = PrimitiveString(item.Description) ?? PrimitiveString(item.Summary),
            SourceUrl = PrimitiveString(item.Link)
        };
    }

    private static Dictionary<string, object> Other(BookResponse item)
    {
        var values = new Dictionary<string, object>();
        if (PrimitiveString(item.Sku) is { } sku) values["sku"] = sku;
        if (PrimitiveString(item.SkuGroup) is { } group) values["skuGroup"] = group;
        return values;
    }

    private static string? PrimitiveString(object? wrapper)
        => wrapper?.GetType().GetProperty("String")?.GetValue(wrapper) as string;

    private static int? PrimitiveInteger(object? wrapper)
        => wrapper?.GetType().GetProperty("Integer")?.GetValue(wrapper) as int?;
}
