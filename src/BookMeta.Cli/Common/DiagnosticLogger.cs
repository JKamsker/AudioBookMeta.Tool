using BookMeta.Configuration;

namespace BookMeta.Common;

public sealed class DiagnosticLogger(ConfigPathResolver paths)
{
    public string Write(Exception exception, string[] arguments)
    {
        var directory = Path.Combine(Path.GetDirectoryName(paths.Resolve(null))!, "logs");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"bookmeta-error-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.log");
        var safeArgs = arguments.Select(RedactArgument);
        File.WriteAllText(path,
            $"time: {DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
            $"command: bookmeta {string.Join(' ', safeArgs)}{Environment.NewLine}" +
            $"config: {paths.Resolve(null)}{Environment.NewLine}" +
            $"exception:{Environment.NewLine}{exception}");
        return path;
    }

    private static string RedactArgument(string value)
        => value.Contains("literal:", StringComparison.OrdinalIgnoreCase) ? "literal:<redacted>" : value;
}
