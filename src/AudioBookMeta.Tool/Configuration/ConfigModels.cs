using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Configuration;

public sealed record AudiobookMetaConfig
{
    public int Version { get; init; } = 1;
    public int? TemplateVersion { get; init; }
    public string? DefaultGroup { get; init; }
    public SearchConfig Search { get; init; } = new();
    public Dictionary<string, ProviderConfig> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Groups { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public required string SourcePath { get; init; }
}

public sealed record SearchConfig
{
    public int Limit { get; init; } = 10;
    public int LimitPerProvider { get; init; } = 10;
    public TimeSpan ProviderTimeout { get; init; } = TimeSpan.FromSeconds(4);
    public TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(8);
    public int MaxConcurrency { get; init; } = 8;
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromMinutes(15);
}

public sealed record ProviderConfig
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required Uri BaseUrl { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Auth { get; init; }
    public string? Region { get; init; }
    public int Priority { get; init; }
    public List<string> Groups { get; init; } = [];
    public TimeSpan? Timeout { get; init; }
    public bool AllowInsecureHttp { get; init; }
    public bool AppendSearchPath { get; init; } = true;
    public bool AllowCrossHostRedirects { get; init; }
    public Dictionary<string, string> QueryParams { get; init; } = [];
    public Dictionary<string, string> Headers { get; init; } = [];
    public Dictionary<string, CapabilityState> CapabilityOverrides { get; init; } = [];
}
