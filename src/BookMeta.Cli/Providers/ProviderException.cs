namespace BookMeta.Providers;

public sealed class ProviderException(string provider, string kind, string message, int? statusCode = null, Exception? inner = null)
    : Exception(message, inner)
{
    public string Provider { get; } = provider;
    public string Kind { get; } = kind;
    public int? StatusCode { get; } = statusCode;

    public string Status => Kind switch
    {
        "timeout" => "timeout",
        "http_rate_limit" => "rate_limited",
        "cancelled" => "timeout",
        _ => "error"
    };
}
