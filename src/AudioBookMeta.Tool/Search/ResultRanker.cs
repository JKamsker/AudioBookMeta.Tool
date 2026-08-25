using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Search;

public sealed class ResultRanker
{
    public IReadOnlyList<SearchResult> Rank(SearchRequest request, IEnumerable<SearchResult> candidates)
    {
        var results = candidates.ToList();
        foreach (var candidate in results)
            Score(request, candidate);
        return results.OrderByDescending(result => result.Score).ThenBy(result => result.Provider, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Score(SearchRequest request, SearchResult result)
    {
        var requestedAsin = TextNormalizer.Identifier(request.Asin);
        var requestedIsbn = TextNormalizer.Identifier(request.Isbn);
        var asins = result.Identifiers.Asin.Select(TextNormalizer.Identifier).ToList();
        var isbns = result.Identifiers.Isbn10.Concat(result.Identifiers.Isbn13).Select(TextNormalizer.Identifier).ToList();
        var identifierMatch = requestedAsin.Length > 0 && asins.Contains(requestedAsin) || requestedIsbn.Length > 0 && isbns.Contains(requestedIsbn);
        var identifierConflict = requestedAsin.Length > 0 && asins.Count > 0 && !asins.Contains(requestedAsin) || requestedIsbn.Length > 0 && isbns.Count > 0 && !isbns.Contains(requestedIsbn);
        if (identifierMatch)
        {
            result.Score = 100;
            result.Confidence = "exact";
            result.ScoreEvidence["identifier"] = "exact match";
            return;
        }

        var titleQuery = request.Title ?? request.Query;
        var fields = new List<(string Name, string? Query, string Candidate, double Weight)>
        {
            ("title", titleQuery, result.Title, 55),
            ("author", request.Author, string.Join(' ', result.Authors), 20),
            ("series", request.Series, string.Join(' ', result.Series.Select(series => series.Name)), 8),
            ("narrator", request.Narrator, string.Join(' ', result.Narrators), 7),
            ("language", request.Language, result.Language ?? string.Empty, 5)
        };
        var applicable = fields.Where(field => !string.IsNullOrWhiteSpace(field.Query) && (field.Name == "title" || !string.IsNullOrWhiteSpace(field.Candidate))).ToList();
        var denominator = applicable.Sum(field => field.Weight);
        var weighted = 0d;
        foreach (var field in applicable)
        {
            var similarity = TextSimilarity.Score(field.Query, field.Candidate, request.Exact);
            weighted += similarity * field.Weight;
            result.ScoreEvidence[field.Name] = $"{similarity:0.00}";
        }
        result.Score = denominator == 0 ? 0 : Math.Round(weighted / denominator * 100, 1);
        var identifierRequested = requestedAsin.Length > 0 || requestedIsbn.Length > 0;
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
}
