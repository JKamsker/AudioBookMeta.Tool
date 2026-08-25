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

    private static SearchResult Result(string title, string author) => new()
    {
        Provider = "test",
        ProviderType = "abs",
        Title = title,
        Authors = [author]
    };
}
