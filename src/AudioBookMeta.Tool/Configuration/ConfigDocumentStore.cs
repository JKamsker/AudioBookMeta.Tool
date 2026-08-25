using System.Globalization;
using AudiobookMeta.Tool.Common;
using Tomlyn;
using Tomlyn.Model;

namespace AudiobookMeta.Tool.Configuration;

public sealed class ConfigDocumentStore(ConfigPathResolver paths)
{
    private static readonly HashSet<string> SearchIntegers = new(StringComparer.Ordinal)
    {
        "limit", "limit_per_provider", "max_concurrency"
    };
    private static readonly HashSet<string> SearchDurations = new(StringComparer.Ordinal)
    {
        "provider_timeout", "deadline", "cache_ttl"
    };
    private static readonly HashSet<string> ProviderStrings = new(StringComparer.Ordinal)
    {
        "type", "base_url", "auth", "region", "timeout"
    };
    private static readonly HashSet<string> ProviderBooleans = new(StringComparer.Ordinal)
    {
        "enabled", "allow_insecure_http", "append_search_path", "allow_cross_host_redirects"
    };
    private static readonly HashSet<string> ProviderMaps = new(StringComparer.Ordinal)
    {
        "query_params", "headers", "capabilities"
    };

    public ConfigValue Get(string? explicitPath, string key)
    {
        var (path, root) = Load(explicitPath, createForMutation: false);
        var parts = ParseKey(key);
        ValidateLeaf(parts);
        var value = Find(root, parts) ?? throw MissingKey(key);
        return new ConfigValue(path, key, Redact(parts, value));
    }

    public ConfigChange Set(string? explicitPath, string key, string value)
    {
        var (path, root) = Load(explicitPath, createForMutation: true);
        var parts = ParseKey(key);
        var parsed = ParseValue(parts, value);
        var table = Traverse(root, parts[..^1], create: true);
        var existed = table.TryGetValue(parts[^1], out var previous);
        table[parts[^1]] = parsed;
        Save(path, root);
        return new ConfigChange(path, key, existed, Redact(parts, previous), Redact(parts, parsed));
    }

    public ConfigChange Unset(string? explicitPath, string key, bool dryRun)
    {
        var (path, root) = Load(explicitPath, createForMutation: false);
        var parts = ParseKey(key);
        ValidateUnset(parts);
        var table = Traverse(root, parts[..^1], create: false);
        if (!table.TryGetValue(parts[^1], out var previous))
            throw MissingKey(key);
        if (!dryRun)
        {
            table.Remove(parts[^1]);
            Save(path, root);
        }
        return new ConfigChange(path, key, true, Redact(parts, previous), null);
    }

    private (string Path, TomlTable Root) Load(string? explicitPath, bool createForMutation)
    {
        var path = paths.Resolve(explicitPath);
        if (!File.Exists(path) && (createForMutation || paths.UsesPlatformDefault(explicitPath)))
            DefaultConfigFile.Create(path);
        if (!File.Exists(path))
            throw new AudiobookMetaException(
                $"Configuration file was not found: {path}", ExitCodes.Configuration,
                "Run a config set command to create it, or select an existing file with BOOKMETA_CONFIG/--config.");
        try
        {
            var root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path), new TomlSerializerOptions { SourceName = path });
            return (path, root ?? throw ConfigError("configuration is empty"));
        }
        catch (TomlException exception)
        {
            throw new AudiobookMetaException(
                $"Configuration is invalid: {exception.Message}", ExitCodes.Configuration,
                "Correct the TOML syntax before changing values through the CLI.", exception);
        }
    }

    private static void Save(string path, TomlTable root)
    {
        try
        {
            var content = TomlSerializer.Serialize(root, new TomlSerializerOptions { WriteIndented = true });
            DefaultConfigFile.WriteAtomically(path, content.EndsWith('\n') ? content : content + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TomlException)
        {
            throw new AudiobookMetaException(
                $"Could not update the configuration file: {path}", ExitCodes.Configuration,
                "Check that the file is writable and valid TOML, then retry.", exception);
        }
    }

    private static object ParseValue(string[] parts, string value)
    {
        ValidateLeaf(parts);
        if (parts is ["version"])
            return Integer(value, parts);
        if (parts is ["default_group"])
            return value;
        if (parts is ["search", var search] && SearchIntegers.Contains(search))
            return Integer(value, parts);
        if (parts is ["search", var duration] && SearchDurations.Contains(duration))
            return Duration(value, parts);
        if (parts is ["groups", _] || parts is ["providers", _, "groups"])
            return StringArray(value, parts);
        if (parts is ["providers", _, "priority"])
            return Integer(value, parts);
        if (parts is ["providers", _, var boolean] && ProviderBooleans.Contains(boolean))
            return Boolean(value, parts);
        if (parts is ["providers", _, "timeout"])
            return Duration(value, parts);
        if (parts is ["providers", _, "base_url"])
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw ValueError(parts, "must be an absolute HTTP or HTTPS URL");
            return value;
        }
        if (parts is ["providers", _, "auth"] && !SecretResolver.HasValidSyntax(value))
            throw ValueError(parts, "must use env:NAME, file:/path, or literal:value syntax");
        if (parts is ["providers", _, "capabilities", _])
            return value switch
            {
                "true" => true,
                "false" => false,
                "unknown" => "unknown",
                _ => throw ValueError(parts, "must be true, false, or unknown")
            };
        return value;
    }

    private static void ValidateLeaf(string[] parts)
    {
        var valid = parts switch
        {
            ["version"] or ["default_group"] => true,
            ["search", var key] => SearchIntegers.Contains(key) || SearchDurations.Contains(key),
            ["groups", _] => true,
            ["providers", _, "groups" or "priority"] => true,
            ["providers", _, var key] => ProviderStrings.Contains(key) || ProviderBooleans.Contains(key),
            ["providers", _, var map, _] => ProviderMaps.Contains(map),
            _ => false
        };
        if (!valid)
            throw ConfigError($"unsupported configuration key '{string.Join('.', parts)}'");
    }

    private static void ValidateUnset(string[] parts)
    {
        if (parts is ["providers", _])
            return;
        ValidateLeaf(parts);
    }

    private static string[] ParseKey(string key)
    {
        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(part => part.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '-')))
            throw ConfigError("keys must use dot-separated letters, digits, underscores, and hyphens");
        return parts;
    }

    private static TomlTable Traverse(TomlTable root, ReadOnlySpan<string> parts, bool create)
    {
        var current = root;
        foreach (var part in parts)
        {
            if (current.TryGetValue(part, out var value) && value is TomlTable table)
                current = table;
            else if (create && !current.ContainsKey(part))
                current = (TomlTable)(current[part] = new TomlTable());
            else
                throw MissingKey(string.Join('.', parts.ToArray()));
        }
        return current;
    }

    private static object? Find(TomlTable root, string[] parts)
    {
        object? current = root;
        foreach (var part in parts)
        {
            if (current is not TomlTable table || !table.TryGetValue(part, out current))
                return null;
        }
        return current;
    }

    private static object Integer(string value, string[] parts)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : throw ValueError(parts, "must be an integer");

    private static object Boolean(string value, string[] parts)
        => bool.TryParse(value, out var parsed) ? parsed : throw ValueError(parts, "must be true or false");

    private static object Duration(string value, string[] parts)
        => DurationParser.TryParse(value, out _) ? value : throw ValueError(parts, "must be a positive duration such as 500ms, 4s, 15m, or 1h");

    private static TomlArray StringArray(string value, string[] parts)
    {
        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0)
            throw ValueError(parts, "must contain one or more comma-separated values");
        var array = new TomlArray();
        foreach (var item in values)
            array.Add(item);
        return array;
    }

    private static object? Redact(string[] parts, object? value)
    {
        if (value is null)
            return null;
        if (parts is ["providers", _, "auth"])
            return "<redacted>";
        if (parts is ["providers", _, "headers", var header] &&
            (header.Contains("token", StringComparison.OrdinalIgnoreCase) ||
             header.Contains("key", StringComparison.OrdinalIgnoreCase) ||
             header.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
             Convert.ToString(value, CultureInfo.InvariantCulture)?.StartsWith("env:", StringComparison.Ordinal) == true ||
             Convert.ToString(value, CultureInfo.InvariantCulture)?.StartsWith("file:", StringComparison.Ordinal) == true ||
             Convert.ToString(value, CultureInfo.InvariantCulture)?.StartsWith("literal:", StringComparison.Ordinal) == true))
            return "<redacted>";
        return value;
    }

    private static AudiobookMetaException MissingKey(string key)
        => new($"Configuration key was not found: {key}", ExitCodes.Configuration, "Run 'dotnet audiobookmeta config get --help' to review supported keys.");

    private static AudiobookMetaException ValueError(string[] parts, string message)
        => ConfigError($"value for '{string.Join('.', parts)}' {message}");

    private static AudiobookMetaException ConfigError(string message)
        => new($"Configuration update is invalid: {message}.", ExitCodes.Configuration, "Run 'dotnet audiobookmeta config set --help' for supported key and value forms.");
}

public sealed record ConfigValue(string Path, string Key, object? Value);
public sealed record ConfigChange(string Path, string Key, bool Replaced, object? PreviousValue, object? Value);
