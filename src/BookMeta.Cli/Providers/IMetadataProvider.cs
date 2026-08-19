using BookMeta.Model;

namespace BookMeta.Providers;

public interface IMetadataProvider
{
    string Id { get; }
    string AdapterType { get; }
    ProviderCapabilities Capabilities { get; }
    Task<ProviderSearchResponse> SearchAsync(SearchRequest request, bool includeRaw, CancellationToken cancellationToken);
    Task<SearchResult> GetAsync(string id, bool includeRaw, CancellationToken cancellationToken);
    Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken);
}
