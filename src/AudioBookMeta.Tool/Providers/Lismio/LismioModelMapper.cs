using System.Text.Json;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.Lismio;

internal sealed class LismioModelMapper(string providerId)
{
    internal SearchResult Map(LismioAudiobook book, string locale, JsonElement? raw)
    {
        var extracted = LismioIdentifierExtractor.Extract(book);
        return new()
        {
        Provider = providerId,
        ProviderType = "lismio",
        ProviderRecordId = book.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Title = book.Title,
        Authors = Prefer(book.Authors, book.Creators),
        Narrators = [.. book.Narrators],
        Contributors = book.Contributors.Select(person => new ContributorEntry(
            person.Name, person.Role, person.Url?.AbsoluteUri)).ToList(),
        Series = book.Series is null ? [] : [new SeriesEntry(book.Series)],
        Identifiers = extracted.Identifiers,
        IdentifierProvenance = extracted.Provenance,
        Publisher = book.Publisher,
        PublishedYear = book.Year,
        ReleaseDate = book.ReleaseDate,
        DurationSeconds = book.DurationMinutes * 60L,
        Format = "audiobook",
        Regions = [locale],
        IsAvailable = book.Shops.Count > 0,
        IsListenable = book.Shops.Count > 0,
        Abridged = book.Abridged,
        Tags = book.Collections.Select(collection => collection.Name)
            .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        CoverUrl = book.CoverUrl?.AbsoluteUri,
        Description = book.Description,
        SourceUrl = book.Url.AbsoluteUri,
        ShortUrl = book.ShortUrl?.AbsoluteUri,
        LinerNotes = book.LinerNotes,
        ShopLinks = MapLinks(book.Shops),
        Versions = book.Versions.Select(version => new AudiobookVersionEntry
        {
            Id = version.Id,
            Ean = version.Ean,
            DurationSeconds = version.DurationMinutes * 60L,
            Abridged = version.Abridged,
            ShopLinks = MapLinks(version.Shops)
        }).ToList(),
        Collections = book.Collections.Select(collection => new CollectionEntry(
            collection.Name, collection.Id, collection.Url.AbsoluteUri)).ToList(),
        Raw = raw
        };
    }

    internal SearchResult MapSummary(
        LismioSummary summary,
        string locale,
        bool includeRaw,
        string? warning = null) => new()
        {
            Provider = providerId,
            ProviderType = "lismio",
            ProviderRecordId = summary.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Title = summary.Title,
            Authors = summary.Creator is null ? [] : [summary.Creator],
            Regions = [locale],
            Format = "audiobook",
            CoverUrl = summary.CoverUrl?.AbsoluteUri,
            SourceUrl = summary.Url.AbsoluteUri,
            Warnings = warning is null ? [] : [warning],
            Raw = includeRaw ? JsonSerializer.SerializeToElement(new { card_html = summary.RawHtml }) : null
        };

    private static List<string> Prefer(IReadOnlyList<string> primary, IReadOnlyList<string> fallback) =>
        (primary.Count > 0 ? primary : fallback).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static List<ShopLinkEntry> MapLinks(IEnumerable<LismioShopLink> links) => links
        .Select(link => new ShopLinkEntry(link.Provider, link.Url.AbsoluteUri))
        .DistinctBy(link => link.Url, StringComparer.OrdinalIgnoreCase)
        .ToList();
}
