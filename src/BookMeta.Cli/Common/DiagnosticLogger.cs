using BookMeta.Configuration;

namespace BookMeta.Common;

public sealed class DiagnosticLogger(ConfigPathResolver paths)
{
    public string Write(Exception exception, string[] arguments)
    {
        var configIndex = Array.IndexOf(arguments, "--config");
        var explicitPath = configIndex >= 0 && configIndex + 1 < arguments.Length ? arguments[configIndex + 1] : null;
        var directory = Path.Combine(Path.GetDirectoryName(paths.Resolve(explicitPath))!, "logs");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"bookmeta-error-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.log");
        var safeArgs = arguments.Select(RedactArgument);
        File.WriteAllText(path,
            $"time: {DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
            $"command: bookmeta {string.Join(' ', safeArgs)}{Environment.NewLine}" +
            $"config: {paths.Resolve(explicitPath)}{Environment.NewLine}" +
            $"exception:{Environment.NewLine}{exception}");
        return path;
    }

    private static string RedactArgument(string value)
        => value.Contains("literal:", StringComparison.OrdinalIgnoreCase) ? "literal:<redacted>" : value;
}
