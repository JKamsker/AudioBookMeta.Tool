using System.Reflection;

namespace AudiobookMeta.Tool.Common;

public static class ApplicationIdentity
{
    private const char BuildMetadataSeparator = '+';

    public const string Command = "dotnet audiobookmeta";
    public static string UserAgent => $"audiobookmeta/{Version}";
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var attribute = typeof(ApplicationIdentity).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var informationalVersion = attribute?.InformationalVersion
            ?? throw new InvalidOperationException("The application assembly has no informational version.");
        var separatorIndex = informationalVersion.IndexOf(BuildMetadataSeparator);

        return separatorIndex < 0
            ? informationalVersion
            : informationalVersion[..separatorIndex];
    }
}
