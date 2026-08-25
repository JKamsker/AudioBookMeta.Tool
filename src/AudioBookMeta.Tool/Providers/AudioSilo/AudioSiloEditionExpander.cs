using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Providers.AudioSilo;

internal static class AudioSiloEditionExpander
{
    private const int MaxConcurrency = 4;

    public static async Task<List<SearchResult>> ExpandAsync(
        IReadOnlyList<string> workIds,
        Func<string, CancellationToken, Task<IReadOnlyList<SearchResult>>> load,
        CancellationToken cancellationToken)
    {
        var results = new IReadOnlyList<SearchResult>[workIds.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, workIds.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken },
            async (index, token) => results[index] = await load(workIds[index], token));
        return results.SelectMany(items => items).ToList();
    }
}
