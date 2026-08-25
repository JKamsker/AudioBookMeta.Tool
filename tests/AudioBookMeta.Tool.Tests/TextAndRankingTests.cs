using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Search;

namespace AudiobookMeta.Tool.Tests;

public sealed class TextAndRankingTests
{
    [Theory]
    [InlineData("The Philosopher’s Stone", "the philosophers stone")]
    [InlineData("J.R.R. Tolkien", "j r r tolkien")]
    [InlineData("  ŻÓŁĆ — Volume 2.5 ", "żółć volume 2 5")]
    public void Normalization_is_unicode_safe(string input, string expected)
        => Assert.Equal(expected, TextNormalizer.Normalize(input));

    [Fact]
    public void Isbn_normalization_removes_separators()
        => Assert.Equal("9780261102217", TextNormalizer.Identifier("978-0-261-10221-7"));

    [Fact]
    public void Incomplete_title_ranks_expected_book_first()
    {
        var candidates = new[]
        {
            Result("A Philosophical History of Pottery", "Other Author"),
            Result("Harry Potter and the Philosopher's Stone", "J. K. Rowling")
        };
        var ranked = new ResultRanker().Rank(new SearchRequest { Query = "har pot philos" }, candidates);
        Assert.Equal("Harry Potter and the Philosopher's Stone", ranked[0].Title);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void Exact_identifier_dominates_text_similarity()
    {
        var identifier = Result("Unrelated title", "Someone") with { Identifiers = new Identifiers { Asin = ["B08G9PRS1K"] } };
        var text = Result("Project Hail Mary", "Andy Weir");
        var ranked = new ResultRanker().Rank(new SearchRequest { Query = "Project Hail Mary", Asin = "B08G9PRS1K" }, [text, identifier]);
        Assert.Same(identifier, ranked[0]);
        Assert.Equal("exact", identifier.Confidence);
    }

    [Fact]
    public void Conflicting_author_prevents_high_confidence()
    {
        var result = Result("Dune", "Not Frank Herbert");
        _ = new ResultRanker().Rank(new SearchRequest { Title = "Dune", Author = "Octavia Butler" }, [result]);
        Assert.NotEqual("high", result.Confidence);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Missing_optional_provider_metadata_does_not_zero_score()
    {
        var result = Result("Dune", string.Empty) with { Authors = [] };
        _ = new ResultRanker().Rank(new SearchRequest { Title = "Dune", Author = "Frank Herbert" }, [result]);
        Assert.Equal(100, result.Score);
        Assert.NotEqual("exact", result.Confidence);
    }

    [Fact]
    public void Duration_ranks_the_matching_edition_above_an_exact_text_match()
    {
        var shorter = Result("Bertrams Hotel", "Agatha Christie") with
        {
            ProviderRecordId = "B00TPKHZ5Y",
            DurationSeconds = 13080
        };
        var matching = Result("Bertrams Hotel", "Agatha Christie") with
        {
            ProviderRecordId = "3844533796",
            DurationSeconds = 22620
        };

        var ranked = new ResultRanker().Rank(new SearchRequest
        {
            Title = "Bertrams Hotel",
            Author = "Agatha Christie",
            DurationSeconds = 22642,
            DurationToleranceSeconds = 90
        }, [shorter, matching]);

        Assert.Same(matching, ranked[0]);
        Assert.True(matching.Score > shorter.Score);
        Assert.Contains("22s difference", matching.ScoreEvidence["duration"], StringComparison.Ordinal);
        Assert.Contains("inside", matching.ScoreEvidence["duration"], StringComparison.Ordinal);
        Assert.Contains("outside", shorter.ScoreEvidence["duration"], StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_sku_match_is_decisive_and_accepts_sku_group()
    {
        var match = Result("Unrelated title", "Someone") with
        {
            Identifiers = new Identifiers { Other = new Dictionary<string, object> { ["skuGroup"] = "BK_HOER_002668" } }
        };
        var text = Result("Bertrams Hotel", "Agatha Christie");

        var ranked = new ResultRanker().Rank(new SearchRequest
        {
            Title = "Bertrams Hotel",
            Author = "Agatha Christie",
            Sku = "bk_hoer_002668"
        }, [text, match]);

        Assert.Same(match, ranked[0]);
        Assert.Equal(100, match.Score);
        Assert.Equal("exact", match.Confidence);
        Assert.Equal("exact SKU/UFID match", match.ScoreEvidence["identifier"]);
    }

    private static SearchResult Result(string title, string author) => new()
    {
        Provider = "test",
        ProviderType = "abs",
        Title = title,
        Authors = [author]
    };
}
