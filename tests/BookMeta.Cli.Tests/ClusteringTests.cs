using BookMeta.Model;
using BookMeta.Search;

namespace BookMeta.Cli.Tests;

public sealed class ClusteringTests
{
    [Fact]
    public void Same_isbn_shares_edition_cluster()
    {
        var results = new[] { Result("p1", "9781427209269", "Ray Porter"), Result("p2", "9781427209269", "Ray Porter") };
        new ResultClusterer().AssignClusters(results, false);
        Assert.Equal(results[0].EditionClusterId, results[1].EditionClusterId);
    }

    [Fact]
    public void Different_recordings_share_work_but_not_edition()
    {
        var first = Result("p1", "9781427209269", "Ray Porter") with { Identifiers = new Identifiers { Asin = ["B000000001"] } };
        var second = Result("p2", "9781427209276", "Other Narrator") with { Identifiers = new Identifiers { Asin = ["B000000002"] } };
        new ResultClusterer().AssignClusters([first, second], false);
        Assert.Equal(first.WorkClusterId, second.WorkClusterId);
        Assert.NotEqual(first.EditionClusterId, second.EditionClusterId);
    }

    private static SearchResult Result(string provider, string isbn, string narrator) => new()
    {
        Provider = provider, ProviderType = "abs", Title = "Project Hail Mary", Authors = ["Andy Weir"], Narrators = [narrator],
        Identifiers = new Identifiers { Isbn13 = [isbn] }
    };
}
