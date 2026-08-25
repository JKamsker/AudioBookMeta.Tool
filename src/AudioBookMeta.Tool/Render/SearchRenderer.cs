using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Search;
using Spectre.Console;

namespace AudiobookMeta.Tool.Render;

public sealed class SearchRenderer(AppConsole console, ResultClusterer clusterer)
{
    public void RenderHuman(SearchResponse response, bool editions, bool noDedupe, bool quiet, bool explain)
    {
        if (quiet)
        {
            foreach (var result in DisplayResults(response.Results, editions || noDedupe))
                console.Out.WriteLine($"{AppConsole.Safe(result.ProviderRecordId ?? result.WorkClusterId)}\t{AppConsole.Safe(result.Title)}");
            WriteWarnings(response, explain);
            return;
        }

        var table = new Table().Border(TableBorder.Simple).AddColumn("#").AddColumn("Score").AddColumn("Title").AddColumn("Author").AddColumn("Narrator").AddColumn("Shops").AddColumn("Sources");
        var rows = editions || noDedupe
            ? response.Results.Select(result => (
                Result: result,
                Sources: result.Provider,
                Narrator: string.Join(", ", result.Narrators),
                Shops: ShopNames([result])))
            : clusterer.WorkGroups(response.Results).Select(group =>
            {
                var best = group[0];
                var narrators = group.SelectMany(result => result.Narrators).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return (
                    Result: best,
                    Sources: string.Join(',', group.Select(result => result.Provider).Distinct(StringComparer.OrdinalIgnoreCase)),
                    Narrator: narrators.Count switch { 0 => string.Empty, 1 => narrators[0], _ => "multiple" },
                    Shops: ShopNames(group));
            });
        var index = 1;
        foreach (var row in rows)
            table.AddRow(
                (index++).ToString(),
                row.Result.Score.ToString("0.#"),
                AppConsole.Safe(row.Result.Title),
                AppConsole.Safe(string.Join(", ", row.Result.Authors)),
                AppConsole.Safe(row.Narrator),
                AppConsole.Safe(row.Shops),
                AppConsole.Safe(row.Sources));
        console.Out.Write(table);
        if (index == 1)
            console.Out.WriteLine("No matches.");

        if (explain)
        {
            console.Out.MarkupLine($"[grey]providers: {AppConsole.Safe(string.Join(',', response.ProviderStatus.Select(status => status.Provider)))}[/]");
            foreach (var result in response.Results)
            {
                console.Out.MarkupLine($"[grey]{AppConsole.Safe(result.Provider)} {AppConsole.Safe(result.Title)}[/]");
                foreach (var evidence in result.ScoreEvidence)
                    console.Out.MarkupLine($"  [grey]{AppConsole.Safe(evidence.Key)}: {AppConsole.Safe(evidence.Value)}[/]");
                console.Out.MarkupLine($"  [grey]cluster: {AppConsole.Safe(result.WorkClusterId)} / {AppConsole.Safe(result.EditionClusterId)}[/]");
            }
        }
        WriteWarnings(response, explain);
    }

    public void RenderJsonLines(SearchResponse response)
    {
        foreach (var result in response.Results)
            console.JsonLine(result);
        foreach (var status in response.ProviderStatus.Where(status => status.Status is not ("ok" or "empty")))
            console.Error($"{status.Provider}: {status.Message}");
    }

    private void WriteWarnings(SearchResponse response, bool explain)
    {
        var failureWarnings = response.ProviderStatus.Where(status => status.Status is not ("ok" or "empty"))
            .Select(status => $"{status.Provider}: {status.Message ?? status.Status}");
        var details = explain ? response.Warnings : [];
        foreach (var warning in failureWarnings.Concat(details).Distinct(StringComparer.Ordinal))
            console.Warning(warning);
    }

    private IEnumerable<SearchResult> DisplayResults(IEnumerable<SearchResult> results, bool editions)
        => editions ? results : clusterer.WorkGroups(results).Select(group => group[0]);

    private static string ShopNames(IEnumerable<SearchResult> results) => string.Join(", ", results
        .SelectMany(result => result.ShopLinks)
        .Select(link => ShopLinkDisplay.Name(link.Provider))
        .Distinct(StringComparer.OrdinalIgnoreCase));
}
