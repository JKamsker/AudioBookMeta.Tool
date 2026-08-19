using BookMeta.Common;

namespace BookMeta.Configuration;

public static class ConfigValidator
{
    private static readonly HashSet<string> AdapterTypes = new(StringComparer.OrdinalIgnoreCase) { "abs", "libex", "audiosilo" };

    public static IReadOnlyList<string> Validate(BookMetaConfig config, bool resolveSecrets)
    {
        var errors = new List<string>();
        if (config.Version != 1)
            errors.Add($"unsupported config version {config.Version}; expected 1");
        if (config.Search.Limit < 1 || config.Search.LimitPerProvider < 1)
            errors.Add("search limits must be positive");
        if (config.Search.MaxConcurrency is < 1 or > 128)
            errors.Add("search.max_concurrency must be between 1 and 128");
        if (config.Search.ProviderTimeout <= TimeSpan.Zero || config.Search.Deadline <= TimeSpan.Zero)
            errors.Add("search timeouts must be positive");

        foreach (var provider in config.Providers.Values)
        {
            if (!AdapterTypes.Contains(provider.Type))
                errors.Add($"provider '{provider.Id}' uses unknown adapter type '{provider.Type}'");
            if (provider.BaseUrl.Scheme is not ("http" or "https"))
                errors.Add($"provider '{provider.Id}' base_url must use HTTP or HTTPS");
            if (provider.BaseUrl.Scheme == "http" && !IsLocal(provider.BaseUrl.Host) && !provider.AllowInsecureHttp)
                errors.Add($"provider '{provider.Id}' uses public HTTP without allow_insecure_http = true");
            if (provider.Timeout is { } providerTimeout && providerTimeout <= TimeSpan.Zero)
                errors.Add($"provider '{provider.Id}' timeout must be positive");
            if (provider.Auth is not null && !SecretResolver.HasValidSyntax(provider.Auth))
                errors.Add($"provider '{provider.Id}' has invalid auth secret reference syntax");
            if (resolveSecrets && provider.Auth is not null)
            {
                try { _ = SecretResolver.Resolve(provider.Auth); }
                catch (BookMetaException exception) { errors.Add($"provider '{provider.Id}': {exception.Message}"); }
            }
            foreach (var header in provider.Headers)
            {
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"provider '{provider.Id}' must use auth instead of headers.Authorization");
                if (resolveSecrets && SecretResolver.HasValidSyntax(header.Value))
                {
                    try { _ = SecretResolver.Resolve(header.Value); }
                    catch (BookMetaException exception) { errors.Add($"provider '{provider.Id}' header '{header.Key}': {exception.Message}"); }
                }
            }
        }

        foreach (var group in config.Groups)
            ValidateGroup(config, group.Key, [], errors);

        if (config.DefaultGroup is not null && !config.Groups.ContainsKey(config.DefaultGroup))
            errors.Add($"default_group '{config.DefaultGroup}' does not exist");
        return errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void ThrowIfInvalid(BookMetaConfig config, bool resolveSecrets)
    {
        var errors = Validate(config, resolveSecrets);
        if (errors.Count > 0)
            throw new BookMetaException($"Configuration is invalid: {string.Join("; ", errors)}.", ExitCodes.Configuration, "Correct the file and run 'bookmeta config validate'.");
    }

    private static void ValidateGroup(BookMetaConfig config, string name, HashSet<string> stack, List<string> errors)
    {
        if (!stack.Add(name))
        {
            errors.Add($"recursive group reference involving '{name}'");
            return;
        }
        foreach (var member in config.Groups[name])
        {
            var groupName = member.StartsWith('@') ? member[1..] : null;
            if (groupName is not null)
            {
                if (!config.Groups.ContainsKey(groupName))
                    errors.Add($"group '{name}' references unknown group '{groupName}'");
                else
                    ValidateGroup(config, groupName, new HashSet<string>(stack, StringComparer.OrdinalIgnoreCase), errors);
            }
            else if (!config.Providers.ContainsKey(member))
                errors.Add($"group '{name}' references unknown provider '{member}'");
        }
    }

    private static bool IsLocal(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
           System.Net.IPAddress.TryParse(host, out var ip) && IsPrivate(ip);

    private static bool IsPrivate(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
            return true;
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] is 10 or 127 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }
}
