using BookMeta.Model;

namespace BookMeta.Providers;

public interface IMetadataProvider
{
    string Id { get; }
    string AdapterType { get; }
    ProviderCapabilities Capabilities { get; }
    Task<ProviderSearchResponse> SearchAsync(SearchRequest request, bool includeRaw, CancellationToken cancellationToken);
    Task<SearchResult> GetAsync(string id, string? region, bool includeRaw, CancellationToken cancellationToken);
    Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken);
}

public interface IAuthorBooksProvider
{
    Task<IReadOnlyList<SearchResult>> GetByAuthorAsync(
        string author,
        string? region,
        bool includeRaw,
        CancellationToken cancellationToken);
}
