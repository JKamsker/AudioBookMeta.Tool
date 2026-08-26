namespace AudiobookMeta.Tool.Providers.Audible;

public static class AudibleMarketplace
{
    private static readonly Dictionary<string, string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        ["au"] = "com.au",
        ["br"] = "com.br",
        ["ca"] = "ca",
        ["de"] = "de",
        ["es"] = "es",
        ["fr"] = "fr",
        ["in"] = "in",
        ["it"] = "it",
        ["jp"] = "co.jp",
        ["uk"] = "co.uk",
        ["us"] = "com"
    };

    public static bool TryNormalize(string? value, out string marketplace)
    {
        marketplace = (value ?? "us").Trim().ToLowerInvariant() switch
        {
            "gb" => "uk",
            "en-gb" => "uk",
            "en-us" => "us",
            "en-ca" => "ca",
            "en-au" => "au",
            "de-de" => "de",
            "es-es" => "es",
            var region => region
        };
        return Domains.ContainsKey(marketplace);
    }

    public static string Domain(string marketplace) => Domains[marketplace];
}
