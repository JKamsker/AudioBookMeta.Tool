using System.Globalization;
using System.Net;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Search;

namespace AudiobookMeta.Tool.Providers.Lismio;

internal sealed record LismioExtractedIdentifiers(
    Identifiers Identifiers,
    List<IdentifierProvenanceEntry> Provenance);

internal static class LismioIdentifierExtractor
{
    internal static LismioExtractedIdentifiers Extract(LismioAudiobook book)
    {
        var eans = new[] { book.Ean }.Concat(book.Versions.Select(version => version.Ean))
            .Select(TextNormalizer.Identifier)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var identifiers = new Identifiers();
        var provenance = new List<IdentifierProvenanceEntry>();

        if (eans.Count == 1)
            identifiers.Other["ean"] = eans[0];
        else if (eans.Count > 1)
            identifiers.Other["ean"] = eans;

        foreach (var ean in eans.Where(IsValidIsbn13))
        {
            identifiers.Isbn13.Add(ean);
            provenance.Add(new("isbn13", ean, "lismio_ean"));
            if (ToIsbn10(ean) is { } isbn10)
            {
                identifiers.Isbn10.Add(isbn10);
                provenance.Add(new("isbn10", isbn10, $"derived_from_isbn13:{ean}"));
            }
        }

        var shopUrls = book.Shops.Select(link => link.Url)
            .Concat(book.Versions.SelectMany(version => version.Shops).Select(link => link.Url));
        foreach (var shopUrl in shopUrls.Distinct())
        {
            if (ConcreteAudibleId(shopUrl) is not { } asin || identifiers.Asin.Contains(asin, StringComparer.OrdinalIgnoreCase))
                continue;
            identifiers.Asin.Add(asin);
            provenance.Add(new("asin", asin, shopUrl.AbsoluteUri));
        }

        return new(identifiers, provenance);
    }

    private static string? ConcreteAudibleId(Uri source)
    {
        var target = UnwrapKnownAffiliate(source);
        if (!IsAudibleHost(target.Host))
            return null;

        var segments = target.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var productIndex = Array.FindIndex(segments, segment => segment.Equals("pd", StringComparison.OrdinalIgnoreCase));
        if (productIndex < 0)
            return null;
        return segments.Skip(productIndex + 1)
            .Reverse()
            .Select(Uri.UnescapeDataString)
            .Select(TextNormalizer.Identifier)
            .FirstOrDefault(value => value.Length == 10 && value.All(char.IsLetterOrDigit));
    }

    private static Uri UnwrapKnownAffiliate(Uri source)
    {
        if (!source.Host.EndsWith("awin1.com", StringComparison.OrdinalIgnoreCase))
            return source;
        foreach (var pair in source.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0 || !WebUtility.UrlDecode(pair[..separator]).Equals("ued", StringComparison.OrdinalIgnoreCase))
                continue;
            var value = WebUtility.UrlDecode(pair[(separator + 1)..]);
            if (Uri.TryCreate(value, UriKind.Absolute, out var target))
                return target;
        }
        return source;
    }

    private static bool IsAudibleHost(string host)
    {
        var normalized = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        return normalized.StartsWith("audible.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidIsbn13(string value)
    {
        if (value.Length != 13 || !value.All(char.IsDigit)
            || !(value.StartsWith("978", StringComparison.Ordinal) || value.StartsWith("979", StringComparison.Ordinal)))
            return false;
        var sum = value.Take(12).Select((character, index) => (character - '0') * (index % 2 == 0 ? 1 : 3)).Sum();
        return (10 - sum % 10) % 10 == value[12] - '0';
    }

    private static string? ToIsbn10(string isbn13)
    {
        if (!IsValidIsbn13(isbn13) || !isbn13.StartsWith("978", StringComparison.Ordinal))
            return null;
        var core = isbn13.Substring(3, 9);
        var sum = core.Select((character, index) => (character - '0') * (10 - index)).Sum();
        var check = (11 - sum % 11) % 11;
        return core + (check == 10 ? "X" : check.ToString(CultureInfo.InvariantCulture));
    }
}
