using BookMeta.Common;

namespace BookMeta.Configuration;

public static class SecretResolver
{
    public static bool HasValidSyntax(string reference)
        => reference.StartsWith("env:", StringComparison.Ordinal) && reference.Length > 4 ||
           reference.StartsWith("file:", StringComparison.Ordinal) && reference.Length > 5 ||
           reference.StartsWith("literal:", StringComparison.Ordinal);

    public static string Resolve(string reference)
    {
        if (!HasValidSyntax(reference))
            throw new BookMetaException("Invalid secret reference syntax.", ExitCodes.Configuration, "Use env:NAME, file:/path, or literal:value.");
        if (reference.StartsWith("env:", StringComparison.Ordinal))
        {
            var name = reference[4..];
            return Environment.GetEnvironmentVariable(name) ?? throw new BookMetaException($"Environment variable '{name}' referenced by auth is not set.", ExitCodes.Configuration, $"Set {name} before running this command.");
        }
        if (reference.StartsWith("file:", StringComparison.Ordinal))
        {
            var path = Environment.ExpandEnvironmentVariables(reference[5..]);
            if (!File.Exists(path))
                throw new BookMetaException($"Secret file was not found: {path}", ExitCodes.Configuration, "Create the file or update the auth reference.");
            return File.ReadAllText(path).TrimEnd('\r', '\n');
        }
        return reference[8..];
    }
}
