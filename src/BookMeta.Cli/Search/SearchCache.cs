using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookMeta.Configuration;
using BookMeta.Model;

namespace BookMeta.Search;

public sealed class SearchCache(ConfigPathResolver paths)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<ProviderSearchResponse?> ReadAsync(ProviderConfig provider, SearchRequest request, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var path = PathFor(provider, request);
        if (!File.Exists(path) || DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path) > ttl)
            return null;
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ProviderSearchResponse>(stream, Options, cancellationToken);
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    public async Task WriteAsync(ProviderConfig provider, SearchRequest request, ProviderSearchResponse response, CancellationToken cancellationToken)
    {
        var path = PathFor(provider, request);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Environment.ProcessId}.tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, response, Options, cancellationToken);
        File.Move(temporary, path, true);
    }

    private string PathFor(ProviderConfig provider, SearchRequest request)
    {
        var payload = $"v1|{provider.Id}|{provider.Type}|{provider.BaseUrl}|{JsonSerializer.Serialize(request, Options)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return Path.Combine(paths.ResolveCacheDirectory(), "search", $"{hash}.json");
    }
}
