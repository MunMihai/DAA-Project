using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Quiz.QuizService.Services;

public sealed class RedisJsonCache(IDistributedCache cache, ILogger<RedisJsonCache> log)
{
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is null) return default;
            return JsonSerializer.Deserialize<T>(bytes, JsonOpt);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Redis GET failed for {Key}", key);
            return default; // fallback: treat as MISS
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpt);
            await cache.SetAsync(
                key,
                bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct
            );
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Redis SET failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Redis REMOVE failed for {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory(ct);
        await SetAsync(key, value, ttl, ct);
        return value;
    }
}
public static class CacheKeys
{
    public static string QuizById(string id) => $"quiz:{id}:full";
    public static string QuizPlay(string id) => $"quiz:{id}:play";

    public static string QuizListVersion() => "quiz:list:ver";

    public static string QuizList(string ver, string status, string tag)
        => $"quiz:list:v={ver}:status={status}:tag={tag}";
}