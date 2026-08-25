using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Model;
using Tomlyn;
using Tomlyn.Model;

namespace AudiobookMeta.Tool.Configuration;

public sealed class ConfigLoader(ConfigPathResolver paths)
{
    public AudiobookMetaConfig Load(string? explicitPath, bool requireFile = true)
    {
        var path = paths.Resolve(explicitPath);
        if (!File.Exists(path) && paths.UsesPlatformDefault(explicitPath))
            DefaultConfigFile.Create(path);
        if (!File.Exists(path))
        {
            if (!requireFile)
                return new AudiobookMetaConfig { SourcePath = path };
            throw new AudiobookMetaException(
                $"Configuration file was not found: {path}", ExitCodes.Configuration,
                "Run a config set command to create it, or set BOOKMETA_CONFIG/--config to an existing TOML file.");
        }

        TomlTable root;
        try
        {
            var parsed = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path), new TomlSerializerOptions { SourceName = path });
            root = parsed ?? throw new AudiobookMetaException("Configuration is empty.", ExitCodes.Configuration, "Add version, providers, and groups to the TOML file.");
        }
        catch (TomlException exception)
        {
            throw new AudiobookMetaException(
                $"Configuration is invalid: {exception.Message}", ExitCodes.Configuration,
                "Run 'dotnet audiobookmeta config validate' after correcting the TOML syntax.", exception);
        }
        var config = new AudiobookMetaConfig
        {
            SourcePath = path,
            Version = Integer(root, "version", 1),
            DefaultGroup = String(root, "default_group"),
            Search = ParseSearch(Table(root, "search")),
            Providers = ParseProviders(Table(root, "providers")),
            Groups = ParseGroups(Table(root, "groups"))
        };
        foreach (var provider in config.Providers.Values)
        {
            foreach (var groupName in provider.Groups)
            {
                if (!config.Groups.TryGetValue(groupName, out var members))
                    config.Groups[groupName] = members = [];
                if (!members.Contains(provider.Id, StringComparer.OrdinalIgnoreCase))
                    members.Add(provider.Id);
            }
        }
        ConfigValidator.ThrowIfInvalid(config, resolveSecrets: false);
        return config;
    }

    private static SearchConfig ParseSearch(TomlTable? table)
    {
        if (table is null)
            return new SearchConfig();

        return new SearchConfig
        {
            Limit = Integer(table, "limit", 10),
            LimitPerProvider = Integer(table, "limit_per_provider", 10),
            ProviderTimeout = Duration(table, "provider_timeout", TimeSpan.FromSeconds(4)),
            Deadline = Duration(table, "deadline", TimeSpan.FromSeconds(8)),
            MaxConcurrency = Integer(table, "max_concurrency", 8),
            CacheTtl = Duration(table, "cache_ttl", TimeSpan.FromMinutes(15))
        };
    }

    private static Dictionary<string, ProviderConfig> ParseProviders(TomlTable? table)
    {
        var providers = new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);
        if (table is null)
            return providers;

        foreach (var (id, value) in table)
        {
            if (value is not TomlTable provider)
                throw ConfigError($"providers.{id} must be a table");
            var type = RequiredString(provider, "type", id);
            var baseUrlText = RequiredString(provider, "base_url", id);
            if (!Uri.TryCreate(baseUrlText, UriKind.Absolute, out var baseUrl))
                throw ConfigError($"providers.{id}.base_url is not an absolute URL");

            providers[id] = new ProviderConfig
            {
                Id = id,
                Type = type.ToLowerInvariant(),
                BaseUrl = NormalizeBaseUrl(baseUrl),
                Enabled = Boolean(provider, "enabled", true),
                Auth = String(provider, "auth"),
                Region = String(provider, "region"),
                Priority = Integer(provider, "priority", 0),
                Groups = StringList(provider, "groups"),
                Timeout = OptionalDuration(provider, "timeout"),
                AllowInsecureHttp = Boolean(provider, "allow_insecure_http", false),
                AppendSearchPath = Boolean(provider, "append_search_path", true),
                AllowCrossHostRedirects = Boolean(provider, "allow_cross_host_redirects", false),
                QueryParams = StringMap(Table(provider, "query_params")),
                Headers = StringMap(Table(provider, "headers")),
                CapabilityOverrides = CapabilityMap(Table(provider, "capabilities"))
            };
        }
        return providers;
    }

    private static Dictionary<string, List<string>> ParseGroups(TomlTable? table)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (table is null)
            return groups;
        foreach (var (name, _) in table)
            groups[name] = StringList(table, name);
        return groups;
    }

    private static Uri NormalizeBaseUrl(Uri uri)
    {
        var builder = new UriBuilder(uri) { Host = uri.Host.ToLowerInvariant() };
        if (builder.Path.Length > 1)
            builder.Path = builder.Path.TrimEnd('/');
        return builder.Uri;
    }

    private static TomlTable? Table(TomlTable table, string key) => table.TryGetValue(key, out var value) ? value as TomlTable : null;
    private static string? String(TomlTable table, string key) => table.TryGetValue(key, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;
    private static string RequiredString(TomlTable table, string key, string id) => String(table, key) ?? throw ConfigError($"providers.{id}.{key} is required");
    private static int Integer(TomlTable table, string key, int fallback) => table.TryGetValue(key, out var value) ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
    private static bool Boolean(TomlTable table, string key, bool fallback) => table.TryGetValue(key, out var value) ? Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;

    private static List<string> StringList(TomlTable table, string key)
        => table.TryGetValue(key, out var value) && value is TomlArray array
            ? array.Select(item => Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).Where(item => item.Length > 0).ToList()
            : [];

    private static Dictionary<string, string> StringMap(TomlTable? table)
        => table?.ToDictionary(pair => pair.Key, pair => Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.OrdinalIgnoreCase) ?? [];

    private static Dictionary<string, CapabilityState> CapabilityMap(TomlTable? table)
    {
        var values = new Dictionary<string, CapabilityState>(StringComparer.OrdinalIgnoreCase);
        if (table is null)
            return values;
        foreach (var (name, value) in table)
            values[name] = value switch { true => CapabilityState.Supported, false => CapabilityState.Unsupported, "unknown" => CapabilityState.Unknown, _ => throw ConfigError($"capability {name} must be true, false, or 'unknown'") };
        return values;
    }

    private static TimeSpan Duration(TomlTable table, string key, TimeSpan fallback)
        => table.TryGetValue(key, out var value) ? ParseDuration(Convert.ToString(value), key) : fallback;
    private static TimeSpan? OptionalDuration(TomlTable table, string key)
        => table.TryGetValue(key, out var value) ? ParseDuration(Convert.ToString(value), key) : null;
    private static TimeSpan ParseDuration(string? value, string key)
        => DurationParser.TryParse(value, out var result) ? result : throw ConfigError($"{key} is not a positive duration");
    private static AudiobookMetaException ConfigError(string message) => new($"Configuration is invalid: {message}.", ExitCodes.Configuration, "Correct the configuration and run 'dotnet audiobookmeta config validate'.");
}
