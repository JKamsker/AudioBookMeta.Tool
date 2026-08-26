using AudiobookMeta.Tool.Configuration;

namespace AudiobookMeta.Tool.Tests;

public sealed class ConfigMigrationTests
{
    [Fact]
    public void Legacy_generated_default_is_reported_without_modifying_the_file()
    {
        var path = LegacyConfig("us");
        var before = File.ReadAllText(path);

        var status = Service().Status(path);

        Assert.Null(status.RecordedTemplateVersion);
        var difference = Assert.Single(status.Differences);
        Assert.Equal("providers.libex.region", difference.Key);
        Assert.Equal("us", difference.CurrentValue);
        Assert.Equal("de", difference.DefaultValue);
        Assert.True(difference.GeneratedDefaultCandidate);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void Dry_run_shows_exact_default_and_metadata_changes_without_writing()
    {
        var path = LegacyConfig("us");
        var before = File.ReadAllText(path);

        var result = Service().Migrate(path, [], applyAllGeneratedDefaults: false, dryRun: true);

        Assert.Equal(2, result.Changes.Count);
        Assert.Contains(result.Changes, change => change.Key == "providers.libex.region"
            && change.PreviousValue == "us" && change.Value == "de");
        Assert.Contains(result.Changes, change => change.Key == "template_version"
            && change.PreviousValue is null && change.Value == "2");
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void Migration_is_idempotent_and_preserves_custom_data_and_credentials()
    {
        var path = LegacyConfig("us");
        var service = Service();

        var first = service.Migrate(path, [], applyAllGeneratedDefaults: true, dryRun: false);
        var afterFirst = File.ReadAllText(path);
        var second = service.Migrate(path, [], applyAllGeneratedDefaults: true, dryRun: false);

        Assert.Equal(2, first.Changes.Count);
        Assert.Empty(second.Changes);
        Assert.Equal(afterFirst, File.ReadAllText(path));
        var loaded = new ConfigLoader(new ConfigPathResolver()).Load(path);
        Assert.Equal(2, loaded.TemplateVersion);
        Assert.Equal("de", loaded.Providers["libex"].Region);
        Assert.Equal("env:LIBEX_TOKEN", loaded.Providers["libex"].Auth);
        Assert.Equal("abs", loaded.Providers["private"].Type);
        Assert.Contains("private", loaded.Groups["extras"]);
    }

    [Fact]
    public void All_preserves_custom_region_but_explicit_apply_adopts_the_default()
    {
        var path = LegacyConfig("uk");
        var service = Service();
        var status = service.Status(path);
        Assert.False(Assert.Single(status.Differences).GeneratedDefaultCandidate);

        var automatic = service.Migrate(path, [], applyAllGeneratedDefaults: true, dryRun: false);
        Assert.DoesNotContain(automatic.Changes, change => change.Key == "providers.libex.region");
        Assert.Equal("uk", new ConfigLoader(new ConfigPathResolver()).Load(path).Providers["libex"].Region);

        var selective = service.Migrate(path, ["providers.libex.region"], applyAllGeneratedDefaults: false, dryRun: false);
        Assert.Contains(selective.Changes, change => change.Key == "providers.libex.region");
        Assert.Equal("de", new ConfigLoader(new ConfigPathResolver()).Load(path).Providers["libex"].Region);
    }

    private static ConfigMigrationService Service() => new(new ConfigPathResolver());

    private static string LegacyConfig(string region)
    {
        var path = Path.Combine(Path.GetTempPath(), $"audiobookmeta-migration-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, $$"""
            version = 1
            default_group = "default"

            [providers.libex]
            type = "libex"
            base_url = "https://libexdb.com"
            region = "{{region}}"
            auth = "env:LIBEX_TOKEN"

            [providers.private]
            type = "abs"
            base_url = "https://private.example"
            enabled = false
            auth = "literal:keep-me"

            [groups]
            default = ["libex"]
            extras = ["private"]
            """);
        return path;
    }
}
