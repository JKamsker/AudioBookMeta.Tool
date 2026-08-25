using System.Net;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Model;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Providers.Abs;
using AudiobookMeta.Tool.Providers.AudioSilo;
using AudiobookMeta.Tool.Providers.Libex;
using AudiobookMeta.Tool.Providers.Lismio;

namespace AudiobookMeta.Tool.Tests;

public sealed class AdapterTests
{
    [Fact]
    public async Task Abs_adapter_normalizes_minimal_and_complete_matches()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/search", request.RequestUri!.AbsolutePath);
            Assert.Contains("query=dune", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Json("""{"matches":[{"title":"Dune","author":"Frank Herbert","narrator":"Simon Vance","isbn":"9780441172719","duration":1234},{"title":"Minimal"}]}"""));
        });
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        var response = await provider.SearchAsync(new SearchRequest { Query = "dune" }, false, TestContext.Current.CancellationToken);
        Assert.Equal(2, response.Candidates.Count);
        Assert.Equal("Frank Herbert", response.Candidates[0].Authors[0]);
        Assert.Equal(1234, response.Candidates[0].DurationSeconds);
    }

    [Fact]
    public async Task Libex_kiota_client_deserializes_search_fixture()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            Assert.Contains(request.RequestUri!.AbsolutePath, new[] { "/quick-search", "/search" });
            return Task.FromResult(TestHttpFactory.Json("""[{"asin":"B08G9PRS1K","title":"Project Hail Mary","region":"us","authors":[{"name":"Andy Weir"}],"narrators":[{"name":"Ray Porter"}],"lengthMinutes":960}]"""));
        });
        var config = Config("libex");
        var provider = new LibexProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));
        var response = await provider.SearchAsync(new SearchRequest { Query = "proj hail", LimitPerProvider = 10 }, false, TestContext.Current.CancellationToken);
        Assert.Equal(2, requests);
        Assert.Equal("Project Hail Mary", response.Candidates[0].Title);
        Assert.Equal("Andy Weir", response.Candidates[0].Authors[0]);
    }

    [Fact]
    public async Task Libex_native_page_bypasses_quick_search_and_forwards_page()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            Assert.Equal("/search", request.RequestUri!.AbsolutePath);
            Assert.Contains("page=3", request.RequestUri.Query, StringComparison.Ordinal);
            Assert.Contains("limit=7", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Json("[]"));
        });
        var config = Config("libex");
        var provider = new LibexProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));

        await provider.SearchAsync(
            new SearchRequest { Query = "dune", LimitPerProvider = 7, Page = 3 },
            false,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Libex_author_books_uses_native_endpoint_and_normalizes_details()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/author/books", request.RequestUri!.AbsolutePath);
            Assert.Contains("name=Andy%20Weir", request.RequestUri.Query, StringComparison.Ordinal);
            Assert.Contains("region=uk", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Json("""
                [{"asin":"B08G9PRS1K","title":"Project Hail Mary","region":"uk","regions":["us","uk"],
                  "rating":4.75,"bookFormat":"audiobook","isAvailable":true,"isBuyable":false,
                  "isListenable":true,"isVvab":false,"authors":[{"name":"Andy Weir"}]}]
                """));
        });
        var config = Config("libex");
        var provider = new LibexProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));

        var books = await provider.GetByAuthorAsync("Andy Weir", "uk", false, TestContext.Current.CancellationToken);

        var book = Assert.Single(books);
        Assert.Equal(4.75, book.Rating);
        Assert.Equal("audiobook", book.Format);
        Assert.Equal(["uk", "us"], book.Regions);
        Assert.True(book.IsAvailable);
        Assert.False(book.IsBuyable);
        Assert.True(book.IsListenable);
        Assert.False(book.IsVirtualVoice);
    }

    [Fact]
    public async Task Libex_get_validates_and_normalizes_asin_before_request()
    {
        var factory = new TestHttpFactory((request, _) =>
        {
            Assert.Equal("/book/B08G9PRS1K", request.RequestUri!.AbsolutePath);
            Assert.Contains("region=uk", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Json("""{"asin":"B08G9PRS1K","title":"Project Hail Mary"}"""));
        });
        var config = Config("libex");
        var provider = new LibexProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));

        var result = await provider.GetAsync("b08g9prs1k", "uk", false, TestContext.Current.CancellationToken);
        Assert.Equal("B08G9PRS1K", result.ProviderRecordId);

        var exception = await Assert.ThrowsAsync<AudiobookMeta.Tool.Common.AudiobookMetaException>(() =>
            provider.GetAsync("too-short", "uk", false, TestContext.Current.CancellationToken));
        Assert.Equal(AudiobookMeta.Tool.Common.ExitCodes.Usage, exception.ExitCode);
    }

    [Fact]
    public async Task AudioSilo_kiota_client_filters_mixed_search_results()
    {
        var factory = new TestHttpFactory((request, _) => Task.FromResult(TestHttpFactory.Json("""
            {"results":[
              {"kind":"person","id":"andy-weir","name":"Andy Weir"},
              {"kind":"work","id":"project-hail-mary","title":"Project Hail Mary","authors":[{"id":"andy-weir","name":"Andy Weir"}],"series":null,"cover_url":null,"added_at":null,"narrators":[{"id":"ray-porter","name":"Ray Porter"}]}
            ]}
            """)));
        var config = Config("audiosilo");
        var provider = new AudioSiloProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));
        var response = await provider.SearchAsync(new SearchRequest { Query = "project hail" }, false, TestContext.Current.CancellationToken);
        Assert.Single(response.Candidates);
        Assert.Equal("Ray Porter", response.Candidates[0].Narrators[0]);
    }

    [Fact]
    public async Task AudioSilo_editions_expand_each_work_recording_with_bounded_detail_requests()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal)
                ? TestHttpFactory.Json("""{"results":[{"kind":"work","id":"the-work","title":"The Work","authors":[],"series":null,"narrators":[]}]}""")
                : TestHttpFactory.Json("""{"id":"the-work","title":"The Work","authors":[],"series":[],"recordings":[{"id":"first","isbn":["9780441172719"],"narrators":[]},{"id":"second","isbn":[],"narrators":[]}]}"""));
        });
        var config = Config("audiosilo");
        var provider = new AudioSiloProvider(config, new ProviderTransport(factory), new KiotaClientFactory(factory));

        var response = await provider.SearchAsync(new SearchRequest { Query = "the work", Editions = true }, false, TestContext.Current.CancellationToken);

        Assert.Equal(2, requests);
        Assert.Equal(2, response.RequestCount);
        Assert.Collection(response.Candidates,
            first => Assert.Equal("work/the-work/recording/first", first.ProviderRecordId),
            second => Assert.Equal("work/the-work/recording/second", second.ProviderRecordId));
    }

    [Fact]
    public async Task Lismio_search_hydrates_complete_metadata_and_shop_links()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
            {
                Assert.Contains("keywords=Project%20Hail%20Mary%20Andy%20Weir", request.RequestUri.Query, StringComparison.Ordinal);
                Assert.Contains("page=3", request.RequestUri.Query, StringComparison.Ordinal);
                return Task.FromResult(TestHttpFactory.Html(LismioSearchHtml));
            }
            Assert.Equal("/de/audiobook/42", request.RequestUri.AbsolutePath);
            return Task.FromResult(TestHttpFactory.Html(LismioDetailHtml));
        });
        var provider = new LismioProvider(Config("lismio"), new ProviderTransport(factory));

        var response = await provider.SearchAsync(
            new SearchRequest
            {
                Title = "Project Hail Mary",
                Author = "Andy Weir",
                Page = 2,
                IncludeShopLinks = true
            },
            true,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, requests);
        Assert.Equal(2, response.RequestCount);
        var book = Assert.Single(response.Candidates);
        Assert.Equal("Andy Weir", Assert.Single(book.Authors));
        Assert.Equal("Ray Porter", Assert.Single(book.Narrators));
        Assert.Equal(5220, book.DurationSeconds);
        Assert.False(book.Abridged);
        Assert.Equal("1234567890123", book.Identifiers.Other["ean"]);
        Assert.Contains(book.ShopLinks, link => link.Provider == "deezer" && link.Url == "https://deezer.com/album/7");
        Assert.Contains(book.ShopLinks, link => link.Provider == "audible" && link.Url.Contains("audible.de", StringComparison.Ordinal));
        Assert.Contains(book.ShopLinks, link => link.Provider == "bookbeat" && link.Url.Contains("bookbeat.de", StringComparison.Ordinal));
        Assert.Equal("A collection", Assert.Single(book.Collections).Name);
        Assert.Contains("Project Hail Mary", book.Raw!.Value.GetProperty("detail_html").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lismio_detail_failure_keeps_search_card_and_reports_warning()
    {
        var factory = new TestHttpFactory((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal)
                ? TestHttpFactory.Html(LismioSearchHtml)
                : TestHttpFactory.Html("<html>changed</html>")));
        var provider = new LismioProvider(Config("lismio"), new ProviderTransport(factory));

        var response = await provider.SearchAsync(
            new SearchRequest { Query = "Project Hail Mary", IncludeShopLinks = true },
            false,
            TestContext.Current.CancellationToken);

        var book = Assert.Single(response.Candidates);
        Assert.Equal("Project Hail Mary", book.Title);
        Assert.Empty(book.ShopLinks);
        Assert.Single(response.Warnings);
        Assert.Single(book.Warnings);
    }

    [Fact]
    public async Task Lismio_search_returns_cards_without_detail_hydration_by_default()
    {
        var requests = 0;
        var factory = new TestHttpFactory((request, _) =>
        {
            requests++;
            Assert.EndsWith("/search", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(TestHttpFactory.Html(LismioSearchHtml));
        });
        var provider = new LismioProvider(Config("lismio"), new ProviderTransport(factory));

        var response = await provider.SearchAsync(
            new SearchRequest { Query = "Project Hail Mary" },
            true,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, requests);
        Assert.Equal(1, response.RequestCount);
        var book = Assert.Single(response.Candidates);
        Assert.Equal("Project Hail Mary", book.Title);
        Assert.Equal("Andy Weir", Assert.Single(book.Authors));
        Assert.Null(book.DurationSeconds);
        Assert.Empty(book.ShopLinks);
        Assert.Contains("Project Hail Mary", book.Raw!.Value.GetProperty("card_html").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lismio_localized_empty_page_is_a_successful_empty_search()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Html(
            "<main><div class=\"ab-grid\"></div><p>Sin resultados</p></main>")));
        var provider = new LismioProvider(Config("lismio"), new ProviderTransport(factory));

        var response = await provider.SearchAsync(
            new SearchRequest { Query = "missing" },
            false,
            TestContext.Current.CancellationToken);

        Assert.Empty(response.Candidates);
        Assert.Equal(1, response.RequestCount);
    }

    [Fact]
    public async Task Authenticated_cross_host_redirect_is_refused()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://evil.example/search") }
        }));
        var config = Config("abs") with { Auth = "literal:secret" };
        var provider = new AbsProvider(config, new ProviderTransport(factory));
        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.SearchAsync(new SearchRequest { Query = "x" }, false, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abs_documented_not_found_can_be_treated_as_empty()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{}", HttpStatusCode.NotFound)));
        var config = Config("abs") with
        {
            CapabilityOverrides = new Dictionary<string, CapabilityState> { ["not_found_is_empty"] = CapabilityState.Supported }
        };
        var response = await new AbsProvider(config, new ProviderTransport(factory))
            .SearchAsync(new SearchRequest { Query = "missing" }, false, TestContext.Current.CancellationToken);
        Assert.Empty(response.Candidates);
        Assert.Single(response.Warnings);
    }

    [Fact]
    public async Task Abs_malformed_matches_shape_is_rejected()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{\"matches\":{}}")));
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        var exception = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.SearchAsync(new SearchRequest { Query = "x" }, false, TestContext.Current.CancellationToken));
        Assert.Equal("invalid_response", exception.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "http_auth")]
    [InlineData(HttpStatusCode.TooManyRequests, "http_rate_limit")]
    [InlineData(HttpStatusCode.InternalServerError, "http_server")]
    public async Task Abs_http_failures_are_classified(HttpStatusCode status, string expectedKind)
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(TestHttpFactory.Json("{}", status)));
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        var exception = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.SearchAsync(new SearchRequest { Query = "x" }, false, TestContext.Current.CancellationToken));
        Assert.Equal(expectedKind, exception.Kind);
    }

    [Fact]
    public async Task Oversized_provider_response_is_rejected_before_parsing()
    {
        var factory = new TestHttpFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[10 * 1024 * 1024 + 1])
        }));
        var provider = new AbsProvider(Config("abs"), new ProviderTransport(factory));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            provider.SearchAsync(new SearchRequest { Query = "x" }, true, TestContext.Current.CancellationToken));
    }

    private static ProviderConfig Config(string type) => new()
    {
        Id = type,
        Type = type,
        BaseUrl = new Uri("https://provider.example"),
        Enabled = true
    };

    private const string LismioSearchHtml = """
        <div class="ab-grid"><div class="appear-in" data-template="tpl-42">
          <a href="https://provider.example/de/audiobook/42/project-hail-mary" aria-label="Audiobook Project Hail Mary">
            <img src="https://images.test/cover.webp"></a>
          <div class="line-clamp-2">Project Hail Mary</div><div class="line-clamp-1">Andy Weir</div>
        </div></div>
        <nav aria-label="Pagination Navigation"><p><span class="font-medium">1</span></p></nav>
        """;

    private const string LismioDetailHtml = """
        <meta name="description" content="A lone astronaut must save humanity.">
        <meta name="image" content="https://images.test/cover.webp">
        <script type="application/ld+json">{"@type":"Audiobook","name":"Project Hail Mary",
          "url":"https://provider.example/de/audiobook/42/project-hail-mary","abridged":false,
          "author":{"name":"Andy Weir"},"offers":[{"url":"https://deezer.com/album/7"}]}</script>
        <h1>Project Hail Mary</h1><a data-testid="audiobook-series-link">Hail Mary</a>
        <div wire:key="version-1">Ungekürzt • 1234567890123 • 1 Stunde 27 Minuten
          <a href="https://www.awin1.com/cread.php?ued=https://www.audible.de/pd/B012345678">Audible</a>
          <a href="https://www.awin1.com/cread.php?ued=https://www.bookbeat.de/buch/42">BookBeat</a></div>
        <a href="https://provider.example/de/artist/1" aria-label="View artist Andy Weir"><div class="line-clamp-1">Autor:in</div></a>
        <a href="https://provider.example/de/artist/2" aria-label="View artist Ray Porter"><div class="line-clamp-1">Sprecher:in</div></a>
        <a href="https://provider.example/de/collection/8"><div class="line-clamp-2">A collection</div></a>
        <section id="tags"><a href="https://provider.example/de/label/2/acme">Acme Audio</a>
          <div data-testid="audiobook-release-date">Veröffentlichungsdatum 13.09.21</div>
          <div data-testid="audiobook-shortlink"><a href="https://lismio.link/abc">short</a></div>
        </section>
        <div data-testid="audiobook-liner-notes">Full publisher description</div>
        """;
}
