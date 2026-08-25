namespace AudiobookMeta.Tool.Search;

public sealed record SearchOptions
{
    public required IReadOnlyList<string> Includes { get; init; }
    public required IReadOnlyList<string> Groups { get; init; }
    public required IReadOnlyList<string> Excludes { get; init; }
    public int? Limit { get; init; }
    public TimeSpan? ProviderTimeout { get; init; }
    public TimeSpan? Deadline { get; init; }
    public bool Fresh { get; init; }
    public bool NoDedupe { get; init; }
    public bool Strict { get; init; }
    public bool IncludeRaw { get; init; }
}
