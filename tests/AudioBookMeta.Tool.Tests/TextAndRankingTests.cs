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
    public void Exact_sku_match_ranks_above_a_regional_sku_group_match()
    {
        var groupMatch = Result("Regional edition", "Someone") with
        {
            ProviderRecordId = "regional",
            Regions = ["ca"],
            Identifiers = new Identifiers
            {
                Other = new Dictionary<string, object>
                {
                    ["sku"] = "BK_HOER_002668CA",
                    ["skuGroup"] = "BK_HOER_002668"
                }
            }
        };
        var exactMatch = Result("Requested edition", "Someone") with
        {
            ProviderRecordId = "exact",
            Regions = ["us"],
            Identifiers = new Identifiers
            {
                Other = new Dictionary<string, object>
                {
                    ["sku"] = "BK_HOER_002668",
                    ["skuGroup"] = "BK_HOER_002668"
                }
            }
        };

        var ranked = new ResultRanker().Rank(new SearchRequest
        {
            Sku = "bk_hoer_002668"
        }, [groupMatch, exactMatch]);

        Assert.Same(exactMatch, ranked[0]);
        Assert.Equal(100, exactMatch.Score);
        Assert.Equal("sku_exact", exactMatch.IdentifierMatchKind);
        Assert.Equal(96, groupMatch.Score);
        Assert.Equal("sku_group", groupMatch.IdentifierMatchKind);
        Assert.Equal("high", groupMatch.Confidence);
    }

    [Fact]
    public void Requested_region_breaks_ties_between_sku_group_matches()
    {
        var german = GroupMatch("de");
        var canadian = GroupMatch("ca");

        var ranked = new ResultRanker().Rank(new SearchRequest
        {
            Sku = "BK_ADBL_054464",
            Region = "de"
        }, [canadian, german]);

        Assert.Same(german, ranked[0]);
        Assert.Equal(98, german.Score);
        Assert.Equal(96, canadian.Score);
        Assert.Contains("preferred region de", german.ScoreEvidence["identifier"], StringComparison.Ordinal);
    }

    [Fact]
    public void Configured_provider_region_breaks_ties_between_sku_group_matches()
    {
        var german = GroupMatch("de") with { ProviderRegion = "de" };
        var canadian = GroupMatch("ca") with { ProviderRegion = "de" };

        var ranked = new ResultRanker().Rank(new SearchRequest { Sku = "BK_ADBL_054464" }, [canadian, german]);

        Assert.Same(german, ranked[0]);
        Assert.Equal(98, german.Score);
    }

    [Fact]
    public void Contaminated_sku_is_downgraded_and_exposes_structured_conflicts()
    {
        var contaminated = Result("Das Recht des Stärkeren", "Hendrik Falkenberg") with
        {
            Narrators = ["Oliver Erwin Schönfeld"],
            DurationSeconds = 39840,
            Identifiers = new Identifiers
            {
                Other = new Dictionary<string, object> { ["sku"] = "BK_ADKO_004186" }
            }
        };

        _ = new ResultRanker().Rank(new SearchRequest
        {
            Title = "GIER - Wie weit würdest du gehen?",
            Author = "Marc Elsberg",
            Narrator = "Dietmar Wunder",
            Sku = "BK_ADKO_004186",
            DurationSeconds = 35235,
            DurationToleranceSeconds = 90
        }, [contaminated]);

        Assert.Equal("sku_exact", contaminated.IdentifierMatchKind);
        Assert.Equal("conflicting_identifier_match", contaminated.MatchAssessment);
        Assert.Equal("low", contaminated.Confidence);
        Assert.True(contaminated.Score <= 60);
        Assert.Equal(["title", "author", "narrator", "duration"], contaminated.Conflicts.Select(conflict => conflict.Field));
        Assert.Contains(contaminated.Warnings, warning => warning.Contains("identifier matched", StringComparison.Ordinal));
    }

    [Fact]
    public void Corroborated_sku_remains_an_exact_match()
    {
        var valid = Result("Bertrams Hotel", "Agatha Christie") with
        {
            Narrators = ["Gabriele Blum"],
            DurationSeconds = 22620,
            Identifiers = new Identifiers
            {
                Other = new Dictionary<string, object> { ["sku"] = "BK_HOER_002668" }
            }
        };

        _ = new ResultRanker().Rank(new SearchRequest
        {
            Title = "Bertrams Hotel",
            Author = "Agatha Christie",
            Narrator = "Gabriele Blum",
            Sku = "BK_HOER_002668",
            DurationSeconds = 22642,
            DurationToleranceSeconds = 90
        }, [valid]);

        Assert.Equal(100, valid.Score);
        Assert.Equal("exact", valid.Confidence);
        Assert.Equal("corroborated_identifier_match", valid.MatchAssessment);
        Assert.Empty(valid.Conflicts);
    }

    private static SearchResult GroupMatch(string region) => Result($"{region} edition", "Someone") with
    {
        ProviderRecordId = region,
        Regions = [region],
        Identifiers = new Identifiers
        {
            Other = new Dictionary<string, object>
            {
                ["sku"] = $"BK_ADBL_054464{region.ToUpperInvariant()}",
                ["skuGroup"] = "BK_ADBL_054464"
            }
        }
    };

    private static SearchResult Result(string title, string author) => new()
    {
        Provider = "test",
        ProviderType = "abs",
        Title = title,
        Authors = [author]
    };
}
