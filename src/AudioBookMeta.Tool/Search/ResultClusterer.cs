using System.Security.Cryptography;
using System.Text;
using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Search;

public sealed class ResultClusterer
{
    public void AssignClusters(IReadOnlyList<SearchResult> results, bool noDedupe)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (noDedupe)
            {
                result.WorkClusterId = $"work:{Hash($"{i}|{result.Provider}|{result.ProviderRecordId}|{result.Title}")}";
                result.EditionClusterId = $"edition:{Hash($"{i}|{result.Provider}|{result.ProviderRecordId}")}";
                continue;
            }
            result.WorkClusterId = FindWorkCluster(results, i) ?? $"work:{Hash(WorkKey(result))}";
            result.EditionClusterId = $"edition:{Hash(EditionKey(result))}";
        }
    }

    public IReadOnlyList<IReadOnlyList<SearchResult>> WorkGroups(IEnumerable<SearchResult> results)
        => results.GroupBy(result => result.WorkClusterId).Select(group => (IReadOnlyList<SearchResult>)group.OrderByDescending(result => result.Score).ToList())
            .OrderByDescending(group => group[0].Score).ToList();

    private static string? FindWorkCluster(IReadOnlyList<SearchResult> results, int index)
    {
        var current = results[index];
        for (var i = 0; i < index; i++)
        {
            var previous = results[i];
            var authorsAgree = current.Authors.Count == 0 || previous.Authors.Count == 0 || current.Authors.Any(a => previous.Authors.Any(b => TextSimilarity.Score(a, b) >= .9));
            if (authorsAgree && TextSimilarity.Score(current.Title, previous.Title) >= .92)
                return previous.WorkClusterId;
        }
        return null;
    }

    private static string WorkKey(SearchResult result)
        => $"{TextNormalizer.Normalize(result.Title)}|{TextNormalizer.Normalize(string.Join(' ', result.Authors))}";

    private static string EditionKey(SearchResult result)
    {
        var asin = result.Identifiers.Asin.Select(TextNormalizer.Identifier).FirstOrDefault(value => value.Length > 0);
        if (asin is not null)
            return $"asin:{asin}";
        var isbn = result.Identifiers.Isbn13.Concat(result.Identifiers.Isbn10).Select(TextNormalizer.Identifier).FirstOrDefault(value => value.Length > 0 && value != "0");
        return isbn is not null
            ? $"isbn:{isbn}"
            : $"{WorkKey(result)}|{TextNormalizer.Normalize(string.Join(' ', result.Narrators))}|{TextNormalizer.Normalize(result.Publisher)}|{result.PublishedYear}";
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
}
