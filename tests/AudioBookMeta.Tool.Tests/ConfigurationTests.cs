using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;

namespace AudiobookMeta.Tool.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Generated_config_selects_only_Libex_by_default_and_allows_explicit_Lismio()
    {
        var path = TemporaryConfig(DefaultConfigFile.Content);
        var config = new ConfigLoader(new ConfigPathResolver()).Load(path);
        var selector = new ProviderSelector();

        var selectedByDefault = selector.Select(config, [], [], []);
        var selectedExplicitly = selector.Select(config, ["lismio"], [], []);

        Assert.Collection(selectedByDefault, provider => Assert.Equal("libex", provider.Id));
        Assert.Collection(selectedExplicitly, provider => Assert.Equal("lismio", provider.Id));
    }

    [Fact]
    public void Loader_merges_provider_group_memberships_and_selector_excludes()
    {
        var path = TemporaryConfig("""
            version = 1
            default_group = "all"
            [providers.one]
            type = "abs"
            base_url = "https://one.example"
            groups = ["all"]
            [providers.two]
            type = "abs"
            base_url = "https://two.example"
            [groups]
            all = ["two"]
            """);
        var config = new ConfigLoader(new ConfigPathResolver()).Load(path);
        var selected = new ProviderSelector().Select(config, [], [], ["two"]);
        Assert.Single(selected);
        Assert.Equal("one", selected[0].Id);
    }

    [Fact]
    public void Recursive_group_is_rejected()
    {
        var path = TemporaryConfig("""
            version = 1
            [providers.one]
            type = "abs"
            base_url = "https://one.example"
            [groups]
            a = ["@b"]
            b = ["@a"]
            """);
        var exception = Assert.Throws<AudiobookMetaException>(() => new ConfigLoader(new ConfigPathResolver()).Load(path));
        Assert.Equal(ExitCodes.Configuration, exception.ExitCode);
        Assert.Contains("recursive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unresolved_environment_secret_is_detected_before_network_use()
    {
        const string variable = "BOOKMETA_TEST_SECRET_THAT_DOES_NOT_EXIST";
        Environment.SetEnvironmentVariable(variable, null);
        var path = TemporaryConfig($"""
            version = 1
            [providers.one]
            type = "abs"
            base_url = "https://one.example"
            auth = "env:{variable}"
            """);
        var config = new ConfigLoader(new ConfigPathResolver()).Load(path);
        var errors = ConfigValidator.Validate(config, resolveSecrets: true);
        Assert.Contains(errors, error => error.Contains(variable, StringComparison.Ordinal));
    }

    [Fact]
    public void Public_plain_http_requires_explicit_opt_in()
    {
        var path = TemporaryConfig("""
            version = 1
            [providers.one]
            type = "abs"
            base_url = "http://one.example"
            """);
        Assert.Throws<AudiobookMetaException>(() => new ConfigLoader(new ConfigPathResolver()).Load(path));
    }

    [Fact]
    public void Lismio_is_a_supported_provider_type()
    {
        var path = TemporaryConfig("""
            version = 1
            [providers.lismio]
            type = "lismio"
            base_url = "https://lismio.app"
            region = "de-DE"
            """);

        var config = new ConfigLoader(new ConfigPathResolver()).Load(path);

        Assert.Equal("lismio", config.Providers["lismio"].Type);
    }

    [Fact]
    public void Audible_is_a_supported_provider_type_with_a_valid_marketplace()
    {
        var path = TemporaryConfig("""
            version = 1
            [providers.audible-es]
            type = "audible"
            base_url = "https://api.audible.es"
            region = "es"
            """);

        var config = new ConfigLoader(new ConfigPathResolver()).Load(path);

        Assert.Equal("audible", config.Providers["audible-es"].Type);
        Assert.Equal("es", config.Providers["audible-es"].Region);
    }

    [Fact]
    public void Audible_requires_an_explicit_supported_marketplace()
    {
        var path = TemporaryConfig("""
            version = 1
            [providers.audible]
            type = "audible"
            base_url = "https://api.audible.de"
            """);

        Assert.Throws<AudiobookMetaException>(() => new ConfigLoader(new ConfigPathResolver()).Load(path));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("-de")]
    [InlineData("de-")]
    [InlineData("de--AT")]
    [InlineData("../../internal")]
    public void Invalid_Lismio_locale_is_rejected_during_configuration_load(string locale)
    {
        var path = TemporaryConfig($$"""
            version = 1
            [providers.lismio]
            type = "lismio"
            base_url = "https://lismio.app"
            region = "{{locale}}"
            """);

        Assert.Throws<AudiobookMetaException>(() => new ConfigLoader(new ConfigPathResolver()).Load(path));
    }

    private static string TemporaryConfig(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"audiobookmeta-test-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, content);
        return path;
    }
}
