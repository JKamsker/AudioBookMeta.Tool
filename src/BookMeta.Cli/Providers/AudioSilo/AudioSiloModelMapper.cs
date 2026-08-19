using BookMeta.Generated.AudioSilo.Models;
using BookMeta.Model;
using NormalizedSearchResult = BookMeta.Model.SearchResult;

namespace BookMeta.Providers.AudioSilo;

internal sealed class AudioSiloModelMapper(string providerId)
{
    public NormalizedSearchResult? MapCard(WorkResult work)
    {
        if (string.IsNullOrWhiteSpace(work.Title))
            return null;
        var series = work.Series?.SeriesRef;
        return new NormalizedSearchResult
        {
            Provider = providerId, ProviderType = "audiosilo", ProviderRecordId = $"work/{work.Id}", Title = work.Title,
            Authors = Names(work.Authors), Narrators = Names(work.Narrators),
            Series = series?.Name is null ? [] : [new SeriesEntry(series.Name, series.Position)],
            Identifiers = new Identifiers(), CoverUrl = work.CoverUrl
        };
    }

    public NormalizedSearchResult? MapDetail(WorkDetail work, string? recordingId)
    {
        if (string.IsNullOrWhiteSpace(work.Title))
            return null;
        var recording = work.Recordings?.FirstOrDefault(item => recordingId is null || item.Id == recordingId);
        return new NormalizedSearchResult
        {
            Provider = providerId, ProviderType = "audiosilo",
            ProviderRecordId = recording is null ? $"work/{work.Id}" : $"work/{work.Id}/recording/{recording.Id}",
            Title = work.Title, Subtitle = work.Subtitle, Authors = Names(work.Authors), Narrators = Names(recording?.Narrators),
            Series = work.Series?.Where(series => series.Name is not null).Select(series => new SeriesEntry(series.Name!, series.Position)).ToList() ?? [],
            Identifiers = RecordingIdentifiers(recording), Publisher = recording?.Publisher, PublishedYear = work.FirstPublished,
            ReleaseDate = recording?.ReleaseDate, Language = work.Language, DurationSeconds = recording?.RuntimeMin is { } minutes ? minutes * 60 : null,
            Genres = work.Genres ?? [], CoverUrl = recording?.CoverUrl, Description = work.Description
        };
    }

    public IReadOnlyList<NormalizedSearchResult> MapEditions(WorkDetail work)
    {
        if (work.Recordings is not { Count: > 0 })
            return MapDetail(work, null) is { } result ? [result] : [];
        return work.Recordings.Select(recording => MapDetail(work, recording.Id))
            .Where(result => result is not null).Select(result => result!).ToList();
    }

    private static List<string> Names(IEnumerable<PersonRef>? people)
        => people?.Select(person => person.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToList() ?? [];

    private static Identifiers RecordingIdentifiers(RecordingDetail? recording)
    {
        var result = new Identifiers();
        if (recording is null)
            return result;
        foreach (var asin in recording.Asin ?? [])
            JsonFields.AddIdentifier(result.Asin, asin.Asin, 10, true);
        foreach (var isbn in recording.Isbn ?? [])
        {
            if (isbn.Length == 10) JsonFields.AddIdentifier(result.Isbn10, isbn, 10);
            if (isbn.Length == 13) JsonFields.AddIdentifier(result.Isbn13, isbn, 13);
        }
        return result;
    }
}
