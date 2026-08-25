namespace AudiobookMeta.Tool.Providers.Lismio;

internal sealed record LismioSummary(
    long Id,
    string Title,
    string? Creator,
    Uri Url,
    Uri? CoverUrl);

internal sealed record LismioPage(
    IReadOnlyList<LismioSummary> Items,
    int Page,
    int Returned,
    int? Total,
    bool HasNext);

internal sealed record LismioContributor(string Name, string Role, Uri? Url);

internal sealed record LismioShopLink(string Provider, Uri Url);

internal sealed record LismioVersion(
    string? Id,
    string? Ean,
    int? DurationMinutes,
    bool? Abridged,
    IReadOnlyList<LismioShopLink> Shops);

internal sealed record LismioCollection(string Name, string? Id, Uri Url);

internal sealed record LismioAudiobook(
    long Id,
    string Title,
    IReadOnlyList<string> Creators,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> Narrators,
    IReadOnlyList<LismioContributor> Contributors,
    string? Series,
    string? Publisher,
    string? Description,
    string? ReleaseDate,
    int? Year,
    int? DurationMinutes,
    string? Ean,
    bool? Abridged,
    Uri? CoverUrl,
    Uri Url,
    Uri? ShortUrl,
    IReadOnlyList<LismioShopLink> Shops,
    string? LinerNotes,
    IReadOnlyList<LismioVersion> Versions,
    IReadOnlyList<LismioCollection> Collections);
