using BookMeta.Configuration;

namespace BookMeta.Providers;

public sealed class ProviderFactory(ProviderTransport transport, KiotaClientFactory kiota)
{
    public IMetadataProvider Create(ProviderConfig config) => config.Type switch
    {
        "abs" => new Abs.AbsProvider(config, transport),
        "libex" => new Libex.LibexProvider(config, transport, kiota),
        "audiosilo" => new AudioSilo.AudioSiloProvider(config, transport, kiota),
        _ => throw new InvalidOperationException($"Unsupported adapter type: {config.Type}")
    };
}
