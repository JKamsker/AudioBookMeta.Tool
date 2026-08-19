using Microsoft.Kiota.Abstractions;

namespace BookMeta.Providers;

internal static class KiotaInvoker
{
    public static async Task<T?> InvokeAsync<T>(string provider, Func<Task<T?>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ApiException exception) when (attempt == 0 && exception.ResponseStatusCode is 429 or 502 or 503 or 504)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (HttpRequestException) when (attempt == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
            catch (ApiException exception)
            {
                throw Map(provider, exception);
            }
            catch (HttpRequestException exception)
            {
                var kind = exception.HttpRequestError switch { HttpRequestError.NameResolutionError => "dns", HttpRequestError.SecureConnectionError => "tls", _ => "http_client" };
                throw new ProviderException(provider, kind, $"request failed: {exception.Message}", inner: exception);
            }
        }
    }

    private static ProviderException Map(string provider, ApiException exception)
    {
        var status = exception.ResponseStatusCode;
        var kind = status switch
        {
            401 or 403 => "http_auth",
            429 => "http_rate_limit",
            >= 500 => "http_server",
            _ => "http_client"
        };
        return new ProviderException(provider, kind, $"provider returned HTTP {status}", status, exception);
    }
}
