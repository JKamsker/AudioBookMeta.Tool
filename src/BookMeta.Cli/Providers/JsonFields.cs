using System.Globalization;
using System.Text.Json;
using BookMeta.Model;
using BookMeta.Search;

namespace BookMeta.Providers;

internal static class JsonFields
{
    public static string? String(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                continue;
            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return value.ToString();
        }
        return null;
    }

    public static long? Integer(JsonElement element, params string[] names)
    {
        var value = String(element, names);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static double? Number(JsonElement element, params string[] names)
    {
        var value = String(element, names);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static bool? Boolean(JsonElement element, params string[] names)
    {
        var value = String(element, names);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    public static List<string> Strings(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : String(item, "name"))
                    .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (value.ValueKind == JsonValueKind.String)
                return SplitPeople(value.GetString());
        }
        return [];
    }

    public static List<string> SplitPeople(string? value)
        => string.IsNullOrWhiteSpace(value) ? [] : value.Split([",", ";", " & "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static List<SeriesEntry> Series(JsonElement element)
    {
        if (!element.TryGetProperty("series", out var value))
            return [];
        if (value.ValueKind == JsonValueKind.String)
            return string.IsNullOrWhiteSpace(value.GetString()) ? [] : [new SeriesEntry(value.GetString()!)];
        if (value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray().Select(item => new SeriesEntry(String(item, "series", "name") ?? string.Empty, String(item, "sequence", "position")))
            .Where(item => item.Name.Length > 0).ToList();
    }

    public static Identifiers Identifiers(string? asin, string? isbn, Dictionary<string, object>? other = null)
    {
        var identifiers = new Identifiers { Other = other ?? [] };
        AddIdentifier(identifiers.Asin, asin, expectedLength: 10, alphanumeric: true);
        foreach (var value in SplitIdentifiers(isbn))
        {
            var normalized = TextNormalizer.Identifier(value);
            if (normalized == "0")
                continue;
            if (normalized.Length == 10)
                identifiers.Isbn10.Add(normalized);
            else if (normalized.Length == 13)
                identifiers.Isbn13.Add(normalized);
        }
        return identifiers;
    }

    public static void AddIdentifier(List<string> target, string? value, int expectedLength, bool alphanumeric = false)
    {
        var normalized = TextNormalizer.Identifier(value);
        if (normalized.Length == expectedLength && (alphanumeric || normalized.All(char.IsDigit)))
            target.Add(normalized);
    }

    private static IEnumerable<string> SplitIdentifiers(string? value)
        => string.IsNullOrWhiteSpace(value) ? [] : value.Split([",", ";", " "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
