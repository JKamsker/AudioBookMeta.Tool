using System.Net;
using System.Text.RegularExpressions;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Model;
using Spectre.Console;

namespace AudiobookMeta.Tool.Render;

public sealed class AudiobookRenderer(AppConsole console)
{
    private const int DescriptionMaximumLength = 500;
    private static readonly Regex HtmlTags = new("<[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RepeatedWhitespace = new("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void RenderList(IReadOnlyList<SearchResult> books)
    {
        var table = new Table().Border(TableBorder.Simple)
            .AddColumn("ASIN")
            .AddColumn("Title")
            .AddColumn("Author")
            .AddColumn("Narrator")
            .AddColumn("Duration")
            .AddColumn("Rating")
            .AddColumn("Region");

        foreach (var book in books)
        {
            table.AddRow(
                AppConsole.Safe(book.ProviderRecordId),
                AppConsole.Safe(book.Title),
                AppConsole.Safe(string.Join(", ", book.Authors)),
                AppConsole.Safe(string.Join(", ", book.Narrators)),
                AppConsole.Safe(FormatDuration(book.DurationSeconds)),
                AppConsole.Safe(book.Rating?.ToString("0.00")),
                AppConsole.Safe(string.Join(", ", book.Regions)));
        }

        console.Out.Write(table);
        console.Out.MarkupLine($"[grey]{books.Count} result(s)[/]");
    }

    public void RenderDetails(SearchResult book)
    {
        var table = new Table().HideHeaders().Border(TableBorder.None);
        table.AddColumn(new TableColumn("Field").RightAligned());
        table.AddColumn("Value");

        Add(table, "Title", book.Title);
        Add(table, "Subtitle", book.Subtitle);
        Add(table, "Provider ID", book.ProviderRecordId);
        Add(table, "Authors", string.Join(", ", book.Authors));
        Add(table, "Narrators", string.Join(", ", book.Narrators));
        Add(table, "Contributors", string.Join(", ", book.Contributors.Select(person => $"{person.Name} ({person.Role})")));
        Add(table, "Series", string.Join(", ", book.Series.Select(FormatSeries)));
        Add(table, "Duration", FormatDuration(book.DurationSeconds));
        Add(table, "Released", book.ReleaseDate);
        Add(table, "Rating", book.Rating?.ToString("0.00"));
        Add(table, "Publisher", book.Publisher);
        Add(table, "Language", book.Language);
        Add(table, "Genres", string.Join(", ", book.Genres));
        Add(table, "Format", book.Format);
        Add(table, "Regions", string.Join(", ", book.Regions));
        Add(table, "Available", FormatBoolean(book.IsAvailable));
        Add(table, "Buyable", FormatBoolean(book.IsBuyable));
        Add(table, "Listenable", FormatBoolean(book.IsListenable));
        Add(table, "Virtual voice", FormatBoolean(book.IsVirtualVoice));
        Add(table, "Abridged", FormatBoolean(book.Abridged));
        Add(table, "EAN", book.Identifiers.Other.TryGetValue("ean", out var ean) ? Convert.ToString(ean) : null);
        Add(table, "Collections", string.Join(", ", book.Collections.Select(collection => collection.Name)));
        Add(table, "Source", book.SourceUrl);
        Add(table, "Short link", book.ShortUrl);
        foreach (var link in book.ShopLinks)
            Add(table, $"Shop: {link.Provider}", link.Url);
        Add(table, "Cover", book.CoverUrl);
        Add(table, "Description", CleanDescription(book.Description));
        Add(table, "Liner notes", CleanDescription(book.LinerNotes));

        console.Out.Write(table);
    }

    private static void Add(Table table, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            table.AddRow($"[grey]{Markup.Escape(name)}[/]", AppConsole.Safe(value));
    }

    private static string FormatSeries(SeriesEntry series)
        => string.IsNullOrWhiteSpace(series.Sequence) ? series.Name : $"{series.Name} #{series.Sequence}";

    private static string? FormatDuration(long? seconds) => seconds switch
    {
        null => null,
        < 3600 => $"{seconds / 60}m",
        _ => $"{seconds / 3600}h {(seconds % 3600) / 60}m"
    };

    private static string? FormatBoolean(bool? value) => value switch
    {
        true => "yes",
        false => "no",
        null => null
    };

    private static string? CleanDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = RepeatedWhitespace.Replace(WebUtility.HtmlDecode(HtmlTags.Replace(value, " ")), " ").Trim();
        return text.Length <= DescriptionMaximumLength ? text : text[..DescriptionMaximumLength] + "…";
    }
}
