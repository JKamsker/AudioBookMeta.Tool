using System.Net;
using System.Text;

namespace BookMeta.Cli.Tests;

internal sealed class TestHttpFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new Handler(responder)) { Timeout = Timeout.InfiniteTimeSpan };

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }
}
