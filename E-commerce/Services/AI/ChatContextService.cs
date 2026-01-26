using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    public class ChatContextService : IChatContextService
    {
        private readonly IDatabase _redis;
        private const string KeyPrefix = "ChatProductStack:";

        public ChatContextService(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();
        }

        public async Task PushProductAsync(string guestId, int productId)
        {
            var key = $"{KeyPrefix}{guestId}";
            await _redis.ListLeftPushAsync(key, productId);
            await _redis.KeyExpireAsync(key, TimeSpan.FromHours(2)); // Same TTL as Cart
        }

        public async Task<int?> PopProductAsync(string guestId)
        {
            var key = $"{KeyPrefix}{guestId}";
            var value = await _redis.ListLeftPopAsync(key);

            if (value.HasValue && int.TryParse(value, out int productId))
            {
                return productId;
            }

            return null;
        }
    }
}
