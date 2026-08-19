using BookMeta.Model;

namespace BookMeta.Search;

public sealed record SearchExecution(SearchResponse Response, int ExitCode);
