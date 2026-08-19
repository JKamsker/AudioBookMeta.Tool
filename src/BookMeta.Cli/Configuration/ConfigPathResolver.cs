namespace BookMeta.Configuration;

public sealed class ConfigPathResolver
{
    public string Resolve(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(explicitPath));

        var fromEnvironment = Environment.GetEnvironmentVariable("BOOKMETA_CONFIG");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(fromEnvironment));

        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bookmeta", "config.toml");

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, "bookmeta", "config.toml");

        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "bookmeta", "config.toml");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bookmeta", "config.toml");
    }

    public string ResolveCacheDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bookmeta", "cache");

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        return !string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(xdg, "bookmeta")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "bookmeta");
    }
}
