using BookMeta.Configuration;
using BookMeta.Model;

namespace BookMeta.Providers;

internal static class CapabilityCatalog
{
    private static readonly string[] Names =
    [
        "search", "free_text_query", "title_filter", "author_filter", "narrator_filter", "series_filter",
        "isbn_filter", "asin_filter", "language_filter", "region_filter", "quick_search", "get_by_id",
        "bulk_get", "chapters", "author_search", "series_search", "native_sort", "native_pagination", "health"
    ];

    public static ProviderCapabilities Create(ProviderConfig config, params string[] supported)
    {
        var supportedSet = supported.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = Names.ToDictionary(name => name, name => supportedSet.Contains(name) ? CapabilityState.Supported : CapabilityState.Unknown, StringComparer.OrdinalIgnoreCase);
        var sources = Names.ToDictionary(name => name, _ => "documented", StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in config.CapabilityOverrides)
        {
            values[name] = value;
            sources[name] = "configured";
        }
        return new ProviderCapabilities { Values = values, Sources = sources };
    }

    public static string Display(CapabilityState state) => state switch
    {
        CapabilityState.Supported => "true",
        CapabilityState.Unsupported => "false",
        _ => "unknown"
    };
}
