using BookMeta.Configuration;
using BookMeta.Generated.AudioSilo;
using BookMeta.Generated.Libex;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace BookMeta.Providers;

public sealed class KiotaClientFactory(IHttpClientFactory clients)
{
    internal LibexApiClient CreateLibex(ProviderConfig config)
        => new(CreateAdapter(config));

    internal AudioSiloApiClient CreateAudioSilo(ProviderConfig config)
        => new(CreateAdapter(config));

    private HttpClientRequestAdapter CreateAdapter(ProviderConfig config)
    {
        var adapter = new HttpClientRequestAdapter(new ProviderAuthenticationProvider(config), httpClient: clients.CreateClient("provider"))
        {
            BaseUrl = config.BaseUrl.ToString().TrimEnd('/')
        };
        return adapter;
    }

    private sealed class ProviderAuthenticationProvider(ProviderConfig config) : IAuthenticationProvider
    {
        public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            request.Headers.TryAdd("User-Agent", "bookmeta/1.0");
            if (config.Auth is not null)
                request.Headers.TryAdd("Authorization", SecretResolver.Resolve(config.Auth));
            foreach (var header in config.Headers)
                request.Headers.TryAdd(header.Key, SecretResolver.HasValidSyntax(header.Value) ? SecretResolver.Resolve(header.Value) : header.Value);
            return Task.CompletedTask;
        }
    }
}
