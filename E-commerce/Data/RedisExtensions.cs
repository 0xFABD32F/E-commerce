using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
namespace E_commerce.Data
{
    public static class RedisExtensions
    {
        public static async Task SetObjectAsync<T>(this IDistributedCache cache, string key, T obj, TimeSpan? expiry = null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromDays(7) // default 7 days
            };
            var json = JsonSerializer.Serialize(obj);
            await cache.SetStringAsync(key, json, options);
        }

        public static async Task<T?> GetObjectAsync<T>(this IDistributedCache cache, string key)
        {
            var json = await cache.GetStringAsync(key);
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }
    }
}

