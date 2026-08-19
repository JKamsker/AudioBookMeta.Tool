using BookMeta.Common;

namespace BookMeta.Configuration;

public sealed class ProviderSelector
{
    public IReadOnlyList<ProviderConfig> Select(BookMetaConfig config, IEnumerable<string> includes, IEnumerable<string> groups, IEnumerable<string> excludes)
    {
        var includeTokens = includes.Concat(groups.Select(group => $"@{group}")).ToList();
        var ids = includeTokens.Count == 0
            ? DefaultProviderIds(config)
            : ExpandTokens(config, includeTokens, allowDisabled: false);
        var excluded = ExpandTokens(config, excludes, allowDisabled: true).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ids.Where(id => !excluded.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => config.Providers[id]).OrderByDescending(provider => provider.Priority).ThenBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> DefaultProviderIds(BookMetaConfig config)
        => config.DefaultGroup is not null
            ? ExpandTokens(config, [$"@{config.DefaultGroup}"], allowDisabled: true).Where(id => config.Providers[id].Enabled)
            : config.Providers.Values.Where(provider => provider.Enabled).Select(provider => provider.Id);

    private static List<string> ExpandTokens(BookMetaConfig config, IEnumerable<string> tokens, bool allowDisabled)
    {
        var result = new List<string>();
        foreach (var token in tokens)
        {
            if (token.StartsWith('@'))
            {
                var name = token[1..];
                if (!config.Groups.TryGetValue(name, out var members))
                    throw Unknown($"provider group '{name}'");
                result.AddRange(ExpandTokens(config, members, allowDisabled));
                continue;
            }
            if (!config.Providers.TryGetValue(token, out var provider))
                throw Unknown($"provider '{token}'");
            if (!provider.Enabled && !allowDisabled)
                throw new BookMetaException($"Provider '{token}' is disabled.", ExitCodes.Configuration, "Enable it in config or select an enabled provider.");
            result.Add(provider.Id);
        }
        return result;
    }

    private static BookMetaException Unknown(string target) => new($"Unknown {target}.", ExitCodes.Configuration, "Run 'bookmeta providers list' to inspect available providers and groups.");
}
