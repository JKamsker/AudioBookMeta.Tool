using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudiobookMeta.Tool.Model;

public sealed record SearchRequest
{
    public string? Query { get; init; }
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Narrator { get; init; }
    public string? Series { get; init; }
    public string? Isbn { get; init; }
    public string? Asin { get; init; }
    public string? Sku { get; init; }
    public string? Publisher { get; init; }
    public long? DurationSeconds { get; init; }
    public long DurationToleranceSeconds { get; init; } = 90;
    public string? Language { get; init; }
    public string? Region { get; init; }
    public int? Page { get; init; }
    public int LimitPerProvider { get; init; } = 10;
    public bool Exact { get; init; }
    public bool IncludeShopLinks { get; init; }

    [JsonIgnore]
    public bool Editions { get; init; }

    [JsonIgnore]
    public bool HasInput => new[] { Query, Title, Author, Narrator, Series, Isbn, Asin, Sku, Publisher }
        .Any(value => !string.IsNullOrWhiteSpace(value));
}

public sealed record SeriesEntry(string Name, string? Sequence = null);

public sealed record ContributorEntry(string Name, string Role, string? Url = null);

public sealed record ShopLinkEntry(string Provider, string Url);

public sealed record CollectionEntry(string Name, string? Id, string Url);

public sealed record AudiobookVersionEntry
{
    public string? Id { get; init; }
    public string? Ean { get; init; }
    public long? DurationSeconds { get; init; }
    public bool? Abridged { get; init; }
    public List<ShopLinkEntry> ShopLinks { get; init; } = [];
}

public sealed record Identifiers
{
    public List<string> Asin { get; init; } = [];
    public List<string> Isbn10 { get; init; } = [];
    public List<string> Isbn13 { get; init; } = [];
    public Dictionary<string, object> Other { get; init; } = [];
}

public sealed record SearchResult
{
    public required string Provider { get; init; }
    public required string ProviderType { get; init; }
    public string? ProviderRecordId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LookupStrategy { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public List<string> Authors { get; init; } = [];
    public List<string> Narrators { get; init; } = [];
    public List<ContributorEntry> Contributors { get; init; } = [];
    public List<SeriesEntry> Series { get; init; } = [];
    public Identifiers Identifiers { get; init; } = new();
    public string? Publisher { get; init; }
    public object? PublishedYear { get; init; }
    public string? ReleaseDate { get; init; }
    public string? Language { get; init; }
    public long? DurationSeconds { get; init; }
    public double? Rating { get; init; }
    public string? Format { get; init; }
    public List<string> Regions { get; init; } = [];
    public bool? IsAvailable { get; init; }
    public bool? IsBuyable { get; init; }
    public bool? IsListenable { get; init; }
    public bool? IsVirtualVoice { get; init; }
    public bool? Abridged { get; init; }
    public List<string> Genres { get; init; } = [];
    public List<string> Tags { get; init; } = [];
    public string? CoverUrl { get; init; }
    public string? Description { get; init; }
    public string? SourceUrl { get; init; }
    public string? ShortUrl { get; init; }
    public string? LinerNotes { get; init; }
    public List<ShopLinkEntry> ShopLinks { get; init; } = [];
    public List<AudiobookVersionEntry> Versions { get; init; } = [];
    public List<CollectionEntry> Collections { get; init; } = [];
    public double Score { get; set; }
    public string? Confidence { get; set; }
    public string? WorkClusterId { get; set; }
    public string? EditionClusterId { get; set; }
    public List<string> Warnings { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Raw { get; init; }

    [JsonIgnore]
    public Dictionary<string, string> ScoreEvidence { get; } = [];
}

public sealed record ProviderStatus
{
    public required string Provider { get; init; }
    public required string Status { get; init; }
    public long ElapsedMs { get; init; }
    public int CandidateCount { get; init; }
    public int RequestCount { get; init; }
    public List<string> LookupStrategies { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}

public sealed record SearchResponse
{
    public int SchemaVersion { get; init; } = 1;
    public required object Request { get; init; }
    public List<SearchResult> Results { get; init; } = [];
    public List<ProviderStatus> ProviderStatus { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}
