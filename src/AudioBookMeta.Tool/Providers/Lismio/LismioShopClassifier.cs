namespace AudiobookMeta.Tool.Providers.Lismio;

internal static class LismioShopClassifier
{
    internal static string FromUrl(Uri url)
    {
        var absolute = Uri.UnescapeDataString(url.AbsoluteUri).ToLowerInvariant();
        if (absolute.Contains("deezer.com", StringComparison.Ordinal)) return "deezer";
        if (absolute.Contains("books.apple.com", StringComparison.Ordinal)) return "apple_books";
        if (absolute.Contains("music.apple.com", StringComparison.Ordinal)) return "apple_music";
        if (absolute.Contains("audible.", StringComparison.Ordinal)) return "audible";
        if (absolute.Contains("open.spotify.com", StringComparison.Ordinal)) return "spotify";
        if (absolute.Contains("music.youtube.com", StringComparison.Ordinal)) return "youtube_music";
        if (absolute.Contains("storytel.com", StringComparison.Ordinal)) return "storytel";
        if (absolute.Contains("play.google.com", StringComparison.Ordinal)) return "google_play_books";
        if (absolute.Contains("music.amazon.", StringComparison.Ordinal)) return "amazon_music";
        if (absolute.Contains("bookbeat.", StringComparison.Ordinal)) return "bookbeat";
        if (absolute.Contains("everand.com", StringComparison.Ordinal)
            || absolute.Contains("scribd.com", StringComparison.Ordinal)) return "everand";
        if (absolute.Contains("kobo.com", StringComparison.Ordinal)) return "kobo";
        if (absolute.Contains("nextory.", StringComparison.Ordinal)) return "nextory";
        if (absolute.Contains("overdrive.com", StringComparison.Ordinal)) return "overdrive";
        if (absolute.Contains("thalia.", StringComparison.Ordinal)) return "thalia";
        return "unknown";
    }
}
