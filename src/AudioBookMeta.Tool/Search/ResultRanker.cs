using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Search;

public sealed class ResultRanker
{
    public IReadOnlyList<SearchResult> Rank(SearchRequest request, IEnumerable<SearchResult> candidates)
    {
        var results = candidates.ToList();
        foreach (var candidate in results)
            Score(request, candidate);
        return results.OrderByDescending(result => result.Score)
            .ThenBy(result => DurationOrder(request, result))
            .ThenBy(result => result.Provider, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Score(SearchRequest request, SearchResult result)
    {
        var requestedAsin = TextNormalizer.Identifier(request.Asin);
        var requestedIsbn = TextNormalizer.Identifier(request.Isbn);
        var requestedSku = TextNormalizer.Identifier(request.Sku);
        var asins = result.Identifiers.Asin.Select(TextNormalizer.Identifier).ToList();
        var isbns = result.Identifiers.Isbn10.Concat(result.Identifiers.Isbn13).Select(TextNormalizer.Identifier).ToList();
        var concreteSkus = IdentifierValues(result.Identifiers.Other, "sku", "ufid").Select(TextNormalizer.Identifier).Where(value => value.Length > 0).ToList();
        var skuGroups = IdentifierValues(result.Identifiers.Other, "skuGroup").Select(TextNormalizer.Identifier).Where(value => value.Length > 0).ToList();
        var exactSkuMatch = requestedSku.Length > 0 && concreteSkus.Contains(requestedSku);
        var skuGroupMatch = requestedSku.Length > 0 && skuGroups.Contains(requestedSku);
        var identifierMatch = requestedAsin.Length > 0 && asins.Contains(requestedAsin)
            || requestedIsbn.Length > 0 && isbns.Contains(requestedIsbn)
            || exactSkuMatch
            || skuGroupMatch;
        var identifierConflict = requestedAsin.Length > 0 && asins.Count > 0 && !asins.Contains(requestedAsin)
            || requestedIsbn.Length > 0 && isbns.Count > 0 && !isbns.Contains(requestedIsbn)
            || requestedSku.Length > 0 && (concreteSkus.Count > 0 || skuGroups.Count > 0) && !exactSkuMatch && !skuGroupMatch;
        if (identifierMatch)
        {
            var preferredRegion = request.Region ?? result.ProviderRegion;
            var preferredGroupMatch = skuGroupMatch
                && !string.IsNullOrWhiteSpace(preferredRegion)
                && result.Regions.Contains(preferredRegion, StringComparer.OrdinalIgnoreCase);
            result.IdentifierMatchKind = exactSkuMatch ? "sku_exact"
                : skuGroupMatch ? "sku_group"
                : requestedAsin.Length > 0 ? "asin_exact"
                : "isbn_exact";
            result.Score = exactSkuMatch || !skuGroupMatch ? 100 : preferredGroupMatch ? 98 : 96;
            result.Confidence = exactSkuMatch || !skuGroupMatch ? "exact" : "high";
            result.ScoreEvidence["identifier"] = exactSkuMatch ? "exact concrete SKU/UFID match"
                : skuGroupMatch ? $"SKU group match{(preferredGroupMatch ? $" in preferred region {preferredRegion}" : string.Empty)}"
                : "exact match";
            AddDurationEvidence(request, result);
            return;
        }

        var titleQuery = request.Title ?? request.Query;
        var fields = new List<(string Name, string? Query, string Candidate, double Weight)>
        {
            ("title", titleQuery, result.Title, 55),
            ("author", request.Author, string.Join(' ', result.Authors), 20),
            ("series", request.Series, string.Join(' ', result.Series.Select(series => series.Name)), 8),
            ("narrator", request.Narrator, string.Join(' ', result.Narrators), 7),
            ("publisher", request.Publisher, result.Publisher ?? string.Empty, 5),
            ("language", request.Language, result.Language ?? string.Empty, 5)
        };
        if (request.DurationSeconds is not null && result.DurationSeconds is not null)
            fields.Add(("duration", "duration", DurationSimilarity(request, result).ToString(System.Globalization.CultureInfo.InvariantCulture), 25));
        var applicable = fields.Where(field => !string.IsNullOrWhiteSpace(field.Query) && (field.Name == "title" || !string.IsNullOrWhiteSpace(field.Candidate))).ToList();
        var denominator = applicable.Sum(field => field.Weight);
        var weighted = 0d;
        foreach (var field in applicable)
        {
            var similarity = field.Name == "duration"
                ? double.Parse(field.Candidate, System.Globalization.CultureInfo.InvariantCulture)
                : TextSimilarity.Score(field.Query, field.Candidate, request.Exact);
            weighted += similarity * field.Weight;
            if (field.Name != "duration")
                result.ScoreEvidence[field.Name] = $"{similarity:0.00}";
        }
        AddDurationEvidence(request, result);
        result.Score = denominator == 0 ? 0 : Math.Round(weighted / denominator * 100, 1);
        var identifierRequested = requestedAsin.Length > 0 || requestedIsbn.Length > 0 || requestedSku.Length > 0;
        if (identifierRequested)
            result.Score = Math.Min(result.Score, 95);
        if (identifierConflict)
        {
            result.Score = Math.Min(result.Score, 70);
            result.Warnings.Add("requested identifier conflicts with this candidate");
        }
        var authorScore = request.Author is null ? 1 : TextSimilarity.Score(request.Author, string.Join(' ', result.Authors), request.Exact);
        result.Confidence = result.Score switch
        {
            >= 99.9 when authorScore >= .99 => "exact",
            >= 90 when authorScore >= .65 && !identifierConflict => "high",
            >= 75 => "medium",
            _ => "low"
        };
        var titleScore = TextSimilarity.Score(titleQuery, result.Title, request.Exact);
        if (request.Author is not null && authorScore < .45 && titleScore >= .8)
            result.Warnings.Add("title is plausible but supplied author evidence is weak");
    }

    private static double DurationSimilarity(SearchRequest request, SearchResult result)
    {
        if (request.DurationSeconds is not { } requested || result.DurationSeconds is not { } actual)
            return 0;
        var difference = Math.Abs(requested - actual);
        var tolerance = Math.Max(1, request.DurationToleranceSeconds);
        if (difference <= tolerance)
            return 1;
        var decayWindow = Math.Max(tolerance * 4d, requested * .1d);
        return Math.Max(0, 1 - (difference - tolerance) / decayWindow);
    }

    private static long DurationOrder(SearchRequest request, SearchResult result)
        => request.DurationSeconds is { } requested && result.DurationSeconds is { } actual
            ? Math.Abs(requested - actual)
            : long.MaxValue;

    private static void AddDurationEvidence(SearchRequest request, SearchResult result)
    {
        if (request.DurationSeconds is not { } requested)
            return;
        if (result.DurationSeconds is not { } actual)
        {
            result.ScoreEvidence["duration"] = "not supplied by provider";
            return;
        }
        var difference = Math.Abs(requested - actual);
        var inside = difference <= request.DurationToleranceSeconds;
        result.ScoreEvidence["duration"] = $"{difference}s difference; {(inside ? "inside" : "outside")} {request.DurationToleranceSeconds}s tolerance";
    }

    private static IEnumerable<string> IdentifierValues(Dictionary<string, object> other, params string[] names)
    {
        foreach (var (key, value) in other)
        {
            if (!names.Contains(key, StringComparer.OrdinalIgnoreCase))
                continue;
            if (value is string text)
                yield return text;
            else if (value is IEnumerable<string> values)
                foreach (var item in values) yield return item;
            else if (value is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.String)
                yield return element.GetString()!;
            else if (value is System.Text.Json.JsonElement array && array.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var item in array.EnumerateArray())
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String) yield return item.GetString()!;
        }
    }
}
