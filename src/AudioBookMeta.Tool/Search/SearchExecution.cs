using AudiobookMeta.Tool.Model;

namespace AudiobookMeta.Tool.Search;

public sealed record SearchExecution(SearchResponse Response, int ExitCode);
