using System.Globalization;

namespace BookMeta.Configuration;

public static class DurationParser
{
    public static bool TryParse(string? value, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        var units = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["ms"] = 0.001,
            ["s"] = 1,
            ["m"] = 60,
            ["h"] = 3600
        };
        foreach (var (suffix, seconds) in units)
        {
            if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (double.TryParse(value[..^suffix.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0)
            {
                duration = TimeSpan.FromSeconds(number * seconds);
                return true;
            }
        }
        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out duration) && duration > TimeSpan.Zero;
    }
}
