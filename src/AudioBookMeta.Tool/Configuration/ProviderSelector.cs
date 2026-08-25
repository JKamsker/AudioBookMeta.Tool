using AudiobookMeta.Tool.Common;

namespace AudiobookMeta.Tool.Configuration;

public sealed class ProviderSelector
{
    public IReadOnlyList<ProviderConfig> Select(AudiobookMetaConfig config, IEnumerable<string> includes, IEnumerable<string> groups, IEnumerable<string> excludes)
    {
        var includeTokens = includes.Concat(groups.Select(group => $"@{group}")).ToList();
        var ids = includeTokens.Count == 0
            ? DefaultProviderIds(config)
            : ExpandTokens(config, includeTokens, allowDisabled: false);
        var excluded = ExpandTokens(config, excludes, allowDisabled: true).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ids.Where(id => !excluded.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => config.Providers[id]).OrderByDescending(provider => provider.Priority).ThenBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> DefaultProviderIds(AudiobookMetaConfig config)
        => config.DefaultGroup is not null
            ? ExpandTokens(config, [$"@{config.DefaultGroup}"], allowDisabled: true).Where(id => config.Providers[id].Enabled)
            : config.Providers.Values.Where(provider => provider.Enabled).Select(provider => provider.Id);

    private static List<string> ExpandTokens(AudiobookMetaConfig config, IEnumerable<string> tokens, bool allowDisabled)
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
                throw new AudiobookMetaException($"Provider '{token}' is disabled.", ExitCodes.Configuration, "Enable it in config or select an enabled provider.");
            result.Add(provider.Id);
        }
        return result;
    }

    private static AudiobookMetaException Unknown(string target) => new($"Unknown {target}.", ExitCodes.Configuration, "Run 'dotnet audiobookmeta providers list' to inspect available providers and groups.");
}
