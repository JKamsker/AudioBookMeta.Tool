namespace AudiobookMeta.Tool.Model;

public enum CapabilityState
{
    Unknown,
    Supported,
    Unsupported
}

public sealed record ProviderCapabilities
{
    public Dictionary<string, CapabilityState> Values { get; init; } = [];
    public Dictionary<string, string> Sources { get; init; } = [];

    public CapabilityState this[string name]
        => Values.GetValueOrDefault(name, CapabilityState.Unknown);
}

public sealed record ProviderSearchResponse
{
    public List<SearchResult> Candidates { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public int RequestCount { get; init; }
}

public sealed record ProviderTestResult(
    string Provider,
    string Status,
    long ElapsedMs,
    string Message);
