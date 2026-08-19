using System.Globalization;
using System.Text;

namespace BookMeta.Search;

public static class TextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var input = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(input.Length);
        var pendingSpace = false;
        foreach (var rune in input.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or
                UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or
                UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                builder.Append(rune);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }
        return builder.ToString().Trim();
    }

    public static string Identifier(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static string[] Tokens(string? value) => Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
