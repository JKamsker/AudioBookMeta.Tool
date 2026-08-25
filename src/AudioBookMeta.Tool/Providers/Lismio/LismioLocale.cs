namespace AudiobookMeta.Tool.Providers.Lismio;

internal static class LismioLocale
{
    internal static bool TryNormalize(string? value, out string locale)
    {
        locale = string.IsNullOrWhiteSpace(value) ? "de" : value.Trim().Trim('/');
        return locale.Split('-').All(segment => segment.Length > 0
            && segment.All(char.IsAsciiLetterOrDigit));
    }
}
