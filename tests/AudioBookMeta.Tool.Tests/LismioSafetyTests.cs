using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Providers.Lismio;

namespace AudiobookMeta.Tool.Tests;

public sealed class LismioSafetyTests
{
    [Fact]
    public async Task Health_uses_a_known_detail_record_instead_of_non_empty_search()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/de/audiobook/23709", request.RequestUri!.AbsolutePath);
            return Task.FromResult(TestHttpFactory.Html(ValidDetailHtml));
        });

        var result = await Provider(factory).TestAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ok", result.Status);
        Assert.Contains("23709", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_still_rejects_a_malformed_detail_page()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Html("<html><main>changed</main></html>")));

        var exception = await Assert.ThrowsAsync<ProviderException>(() =>
            Provider(factory).TestAsync(TestContext.Current.CancellationToken));

        Assert.Equal("invalid_response", exception.Kind);
        Assert.Contains("title", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_probe_record_is_configurable_and_not_forwarded_as_query_data()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/de/audiobook/74013", request.RequestUri!.AbsolutePath);
            Assert.DoesNotContain("health_probe_id", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Html(ValidDetailHtml));
        });
        var provider = Provider(factory, new Dictionary<string, string> { ["health_probe_id"] = "74013" });

        var result = await provider.TestAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ok", result.Status);
        Assert.Contains("74013", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Identifier_extraction_rejects_audible_and_affiliate_lookalike_hosts()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Html("""
            <script type="application/ld+json">{"@type":"Audiobook","name":"Example"}</script>
            <h1>Example</h1>
            <div wire:key="version-1">
              <a href="https://audible.com.evil.example/pd/B084WH8CFN">Fake Audible</a>
              <a href="https://evilawin1.com/cread.php?ued=https%3A%2F%2Fwww.audible.de%2Fpd%2FB084WH8CFN">Fake AWIN</a>
            </div>
            """)));

        var result = await Provider(factory).GetAsync("1", "de", false, TestContext.Current.CancellationToken);

        Assert.Empty(result.Identifiers.Asin);
    }

    private static LismioProvider Provider(IHttpClientFactory http, Dictionary<string, string>? query = null) => new(new ProviderConfig
    {
        Id = "lismio",
        Type = "lismio",
        BaseUrl = new Uri("https://provider.example"),
        Region = "de",
        Enabled = true,
        QueryParams = query ?? []
    }, new ProviderTransport(http));

    private const string ValidDetailHtml = """
        <script type="application/ld+json">{"@type":"Audiobook","name":"Example"}</script>
        <h1>Example</h1>
        """;
}
