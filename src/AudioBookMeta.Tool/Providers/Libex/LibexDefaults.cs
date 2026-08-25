using AudiobookMeta.Tool.Common;

namespace AudiobookMeta.Tool.Providers.Libex;

internal static class LibexDefaults
{
    public const int MaximumSearchLimit = 50;
    public const int DefaultSearchPage = 0;
    public const int AsinLength = 10;
}

internal static class LibexAsin
{
    public static string Normalize(string value)
    {
        var asin = value.Trim().ToUpperInvariant();
        if (asin.Length != LibexDefaults.AsinLength || !asin.All(char.IsLetterOrDigit))
        {
            throw new AudiobookMetaException(
                "A Libex ASIN must contain exactly ten letters or digits.",
                ExitCodes.Usage,
                "Check the Audible ASIN and retry with 'dotnet audiobookmeta get PROVIDER:ASIN'.");
        }

        return asin;
    }
}
