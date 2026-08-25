namespace AudiobookMeta.Tool.Providers;

public static class ProviderNotes
{
    public static string ForType(string type) => type switch
    {
        "abs" => "Generic Audiobookshelf search-only provider; optional fields and fuzzy behavior vary by deployment.",
        "libex" => "Native Libex adapter with quick search, structured search, ASIN lookup, and health checks.",
        "audiosilo" => "Native AudioSilo Meta adapter with prefix search, identifier lookup, and work/recording identity.",
        _ => "No source notes available."
    };
}
