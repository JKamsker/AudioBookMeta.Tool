using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;

namespace AudiobookMeta.Tool.Tests;

public sealed class ConfigurationTests
{
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
            region = "de"
            """);

        var config = new ConfigLoader(new ConfigPathResolver()).Load(path);

        Assert.Equal("lismio", config.Providers["lismio"].Type);
    }

    [Fact]
    public void Lismio_locale_is_validated_during_configuration_load()
    {
        var path = TemporaryConfig("""
            version = 1
            [providers.lismio]
            type = "lismio"
            base_url = "https://lismio.app"
            region = "../../internal"
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
