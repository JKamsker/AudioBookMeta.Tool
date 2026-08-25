using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;

namespace AudiobookMeta.Tool.Providers;

public sealed record TransportResponse(byte[] Content, Uri Uri, HttpStatusCode StatusCode, long ElapsedMs);

public sealed class ProviderTransport(IHttpClientFactory clients)
{
    private const int MaxResponseBytes = 10 * 1024 * 1024;

    public async Task<TransportResponse> GetAsync(ProviderConfig provider, Uri uri, CancellationToken cancellationToken)
    {
        var auth = provider.Auth is null ? null : SecretResolver.Resolve(provider.Auth);
        var current = uri;
        var retries = 0;
        var redirects = 0;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd(ApplicationIdentity.UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (auth is not null)
                request.Headers.TryAddWithoutValidation("Authorization", auth);
            foreach (var header in provider.Headers)
                request.Headers.TryAddWithoutValidation(header.Key, ResolveHeaderValue(header.Value));

            HttpResponseMessage response;
            try
            {
                response = await clients.CreateClient("provider").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException) when (retries++ < 1)
            {
                continue;
            }
            catch (HttpRequestException exception)
            {
                throw new ProviderException(provider.Id, ClassifyTransport(exception), $"request to {Redact(current)} failed: {exception.Message}", inner: exception);
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode) && response.Headers.Location is not null && redirects++ < 5)
                {
                    var target = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
                    if (auth is not null && !SameOrigin(current, target) && !provider.AllowCrossHostRedirects)
                        throw new ProviderException(provider.Id, "http_client", $"authenticated redirect from {current.Host} to {target.Host} was refused", (int)response.StatusCode);
                    current = target;
                    continue;
                }

                if (ShouldRetry(response.StatusCode) && retries++ < 1 && await CanRetryAsync(response, cancellationToken))
                    continue;

                var content = await ReadLimitedAsync(response.Content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw HttpFailure(provider.Id, current, response.StatusCode);
                return new TransportResponse(content, current, response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        }
    }

    public static Uri BuildUri(Uri baseUrl, string? appendPath, IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var builder = new UriBuilder(baseUrl);
        if (!string.IsNullOrEmpty(appendPath))
            builder.Path = $"{builder.Path.TrimEnd('/')}/{appendPath.TrimStart('/')}";
        var pairs = ParseQuery(builder.Query).Concat(parameters.Where(pair => pair.Value is not null)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!)));
        builder.Query = string.Join('&', pairs.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return builder.Uri;
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxResponseBytes)
            throw new InvalidDataException($"provider response exceeds {MaxResponseBytes} bytes");
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            if (output.Length + read > MaxResponseBytes)
                throw new InvalidDataException($"provider response exceeds {MaxResponseBytes} bytes");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static ProviderException HttpFailure(string provider, Uri uri, HttpStatusCode status)
    {
        var code = (int)status;
        var kind = status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "http_auth",
            HttpStatusCode.TooManyRequests => "http_rate_limit",
            >= HttpStatusCode.InternalServerError => "http_server",
            _ => "http_client"
        };
        return new ProviderException(provider, kind, $"provider returned HTTP {code} for {Redact(uri)}", code);
    }

    private static string ClassifyTransport(HttpRequestException exception)
        => exception.HttpRequestError switch { HttpRequestError.NameResolutionError => "dns", HttpRequestError.SecureConnectionError => "tls", _ => "http_client" };
    private static bool ShouldRetry(HttpStatusCode status) => status is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
    private static bool IsRedirect(HttpStatusCode status) => (int)status is 301 or 302 or 303 or 307 or 308;
    private static bool SameOrigin(Uri left, Uri right) => left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) && left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;
    private static string Redact(Uri uri) => new UriBuilder(uri) { Query = string.Empty }.Uri.ToString();
    private static string ResolveHeaderValue(string value) => SecretResolver.HasValidSyntax(value) ? SecretResolver.Resolve(value) : value;

    private static async Task<bool> CanRetryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.TooManyRequests || response.Headers.RetryAfter is null)
            return true;
        var delay = response.Headers.RetryAfter.Delta ?? response.Headers.RetryAfter.Date - DateTimeOffset.UtcNow;
        if (delay is null || delay <= TimeSpan.Zero)
            return true;
        if (delay > TimeSpan.FromSeconds(2))
            return false;
        await Task.Delay(delay.Value, cancellationToken);
        return true;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            yield return new KeyValuePair<string, string>(Uri.UnescapeDataString(pieces[0]), pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : string.Empty);
        }
    }
}
