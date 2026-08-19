namespace BookMeta.Search;

public static class TextSimilarity
{
    public static double Score(string? query, string? candidate, bool exact = false)
    {
        var left = TextNormalizer.Normalize(query);
        var right = TextNormalizer.Normalize(candidate);
        if (left.Length == 0 || right.Length == 0)
            return 0;
        if (left == right)
            return 1;
        if (exact)
            return 0;

        var queryTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var candidateTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var prefix = queryTokens.Count(queryToken => candidateTokens.Any(candidate => candidate.StartsWith(queryToken, StringComparison.Ordinal))) / (double)queryTokens.Length;
        var ordered = OrderedCoverage(queryTokens, candidateTokens);
        var edit = 1 - Levenshtein(left, right) / (double)Math.Max(left.Length, right.Length);
        var intersection = queryTokens.Intersect(candidateTokens, StringComparer.Ordinal).Count();
        var union = queryTokens.Union(candidateTokens, StringComparer.Ordinal).Count();
        var tokenSet = union == 0 ? 0 : intersection / (double)union;
        return Math.Clamp(0.40 * prefix + 0.30 * ordered + 0.20 * edit + 0.10 * tokenSet, 0, 1);
    }

    private static double OrderedCoverage(string[] query, string[] candidate)
    {
        var next = 0;
        var matched = 0;
        foreach (var queryToken in query)
        {
            while (next < candidate.Length && !candidate[next].StartsWith(queryToken, StringComparison.Ordinal))
                next++;
            if (next >= candidate.Length)
                break;
            matched++;
            next++;
        }
        return matched / (double)query.Length;
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
