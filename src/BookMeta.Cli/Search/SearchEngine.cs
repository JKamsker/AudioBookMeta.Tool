using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using BookMeta.Common;
using BookMeta.Configuration;
using BookMeta.Model;
using BookMeta.Providers;

namespace BookMeta.Search;

public sealed class SearchEngine(
    ProviderSelector selector,
    ProviderFactory factory,
    SearchCache cache,
    ResultRanker ranker,
    ResultClusterer clusterer)
{
    public async Task<SearchExecution> ExecuteAsync(BookMetaConfig config, SearchRequest request, SearchOptions options, CancellationToken cancellationToken)
    {
        var providers = selector.Select(config, options.Includes, options.Groups, options.Excludes);
        if (providers.Count == 0)
            throw new BookMetaException("No enabled providers were selected.", ExitCodes.Configuration, "Enable a provider or select a non-empty provider group.");

        var candidates = new ConcurrentBag<SearchResult>();
        var statuses = new ConcurrentBag<ProviderStatus>();
        var warnings = new ConcurrentBag<string>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Deadline ?? config.Search.Deadline);
        using var concurrency = new SemaphoreSlim(config.Search.MaxConcurrency);
        var tasks = providers.Select(provider => RunProviderAsync(provider, config, request, options, candidates, statuses, warnings, concurrency, deadline.Token)).ToList();
        await Task.WhenAll(tasks);

        var ranked = ranker.Rank(request, candidates);
        clusterer.AssignClusters(ranked, options.NoDedupe);
        var limit = options.Limit ?? config.Search.Limit;
        var limited = Limit(ranked, limit, options.NoDedupe);
        var orderedStatuses = statuses.OrderBy(status => providers.ToList().FindIndex(provider => provider.Id.Equals(status.Provider, StringComparison.OrdinalIgnoreCase))).ToList();
        var failures = orderedStatuses.Count(status => status.Status is not ("ok" or "empty"));
        var successes = orderedStatuses.Count - failures;
        var exitCode = successes == 0 ? ExitCodes.ProvidersFailed : options.Strict && failures > 0 ? ExitCodes.StrictFailure : ExitCodes.Success;
        var response = new SearchResponse
        {
            Request = RequestOutput(request, providers.Select(provider => provider.Id).ToList()),
            Results = limited.ToList(), ProviderStatus = orderedStatuses,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList()
        };
        return new SearchExecution(response, exitCode);
    }

    private async Task RunProviderAsync(
        ProviderConfig provider,
        BookMetaConfig config,
        SearchRequest request,
        SearchOptions options,
        ConcurrentBag<SearchResult> candidates,
        ConcurrentBag<ProviderStatus> statuses,
        ConcurrentBag<string> warnings,
        SemaphoreSlim concurrency,
        CancellationToken deadline)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            await concurrency.WaitAsync(deadline);
            try
            {
                var cached = options.Fresh ? null : await cache.ReadAsync(provider, request, config.Search.CacheTtl, deadline);
                var response = cached;
                var cacheHit = cached is not null;
                if (response is null)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline);
                    timeout.CancelAfter(options.ProviderTimeout ?? provider.Timeout ?? config.Search.ProviderTimeout);
                    response = await factory.Create(provider).SearchAsync(request, options.IncludeRaw, timeout.Token);
                    if (!options.IncludeRaw)
                        await cache.WriteAsync(provider, request, response, deadline);
                }
                foreach (var candidate in response.Candidates.Take(request.LimitPerProvider * 2))
                    candidates.Add(candidate);
                foreach (var warning in response.Warnings)
                    warnings.Add($"{provider.Id}: {warning}");
                statuses.Add(new ProviderStatus
                {
                    Provider = provider.Id, Status = response.Candidates.Count == 0 ? "empty" : "ok", ElapsedMs = timer.ElapsedMilliseconds,
                    CandidateCount = response.Candidates.Count, RequestCount = cacheHit ? 0 : response.RequestCount,
                    Message = cacheHit ? "cache hit" : null
                });
            }
            finally { concurrency.Release(); }
        }
        catch (OperationCanceledException)
        {
            statuses.Add(new ProviderStatus { Provider = provider.Id, Status = "timeout", ElapsedMs = timer.ElapsedMilliseconds, Message = "provider timed out" });
            warnings.Add($"{provider.Id}: provider timed out");
        }
        catch (ProviderException exception)
        {
            statuses.Add(new ProviderStatus { Provider = provider.Id, Status = exception.Status, ElapsedMs = timer.ElapsedMilliseconds, Message = exception.Message });
            warnings.Add($"{provider.Id}: {exception.Message}");
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException)
        {
            statuses.Add(new ProviderStatus { Provider = provider.Id, Status = "error", ElapsedMs = timer.ElapsedMilliseconds, Message = "provider returned an invalid response" });
            warnings.Add($"{provider.Id}: provider returned an invalid response ({exception.Message})");
        }
    }

    private IReadOnlyList<SearchResult> Limit(IReadOnlyList<SearchResult> results, int limit, bool noDedupe)
    {
        if (noDedupe)
            return results.Take(limit).ToList();
        var groups = clusterer.WorkGroups(results).Take(limit).ToList();
        var allowed = groups.Select(group => group[0].WorkClusterId).ToHashSet(StringComparer.Ordinal);
        return results.Where(result => allowed.Contains(result.WorkClusterId)).ToList();
    }

    private static object RequestOutput(SearchRequest request, List<string> providers) => new
    {
        request.Query, request.Title, request.Author, request.Narrator, request.Series, request.Isbn, request.Asin,
        request.Language, request.Region, request.LimitPerProvider, request.Exact, Providers = providers
    };
}
