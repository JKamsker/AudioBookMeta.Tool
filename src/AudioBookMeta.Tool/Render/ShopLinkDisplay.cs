namespace AudiobookMeta.Tool.Render;

internal static class ShopLinkDisplay
{
    internal static string Name(string provider) => provider switch
    {
        "amazon_music" => "Amazon Music",
        "apple_books" => "Apple Books",
        "apple_music" => "Apple Music",
        "audible" => "Audible",
        "bookbeat" => "BookBeat",
        "deezer" => "Deezer",
        "everand" => "Everand",
        "google_play_books" => "Google Play Books",
        "kobo" => "Kobo",
        "nextory" => "Nextory",
        "overdrive" => "OverDrive",
        "spotify" => "Spotify",
        "storytel" => "Storytel",
        "thalia" => "Thalia",
        "youtube_music" => "YouTube Music",
        "unknown" => "Unknown",
        _ => provider
    };
}
