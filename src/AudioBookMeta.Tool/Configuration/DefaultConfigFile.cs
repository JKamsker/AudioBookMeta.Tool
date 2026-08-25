using AudiobookMeta.Tool.Common;

namespace AudiobookMeta.Tool.Configuration;

public static class DefaultConfigFile
{
    public const string Content = """
        version = 1
        default_group = "default"

        [search]
        limit = 10
        limit_per_provider = 10
        provider_timeout = "4s"
        deadline = "8s"
        max_concurrency = 8
        cache_ttl = "15m"

        [providers.libex]
        type = "libex"
        base_url = "https://libexdb.com"
        enabled = true
        region = "us"
        priority = 100
        groups = ["default", "audiobook"]

        [providers.audiosilo]
        type = "audiosilo"
        base_url = "https://meta.audiosilo.app"
        enabled = true
        priority = 95
        groups = ["default", "audiobook", "open-data"]

        [groups]
        default = ["libex", "audiosilo"]
        audiobook = ["libex", "audiosilo"]
        open-data = ["audiosilo"]
        """;

    public static bool Create(string path, bool overwrite = false)
    {
        if (!overwrite && File.Exists(path))
            return false;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (overwrite)
                WriteAtomically(path, Content + Environment.NewLine);
            else
                WriteNew(path, Content + Environment.NewLine);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AudiobookMetaException(
                $"Could not create the configuration file: {path}", ExitCodes.Configuration,
                "Check that the parent directory is writable or select another path with --config.", exception);
        }
    }

    public static void WriteAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        UnixFileMode? mode = null;
        if (!OperatingSystem.IsWindows() && File.Exists(path))
            mode = File.GetUnixFileMode(path);

        try
        {
            File.WriteAllText(temporaryPath, content);
            Protect(temporaryPath, mode);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void WriteNew(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void Protect(string path, UnixFileMode? existingMode)
    {
        if (OperatingSystem.IsWindows())
            return;
        File.SetUnixFileMode(path, existingMode ?? (UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }
}
