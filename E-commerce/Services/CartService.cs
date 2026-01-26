using E_commerce.Data;
using E_commerce.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace E_commerce.Services
{
    public class CartService : ICartService
    {
        private readonly IDatabase _redis;
        private readonly E_commerceContext _context;
        private const int CartTtlHours = 2;

        public CartService(IConnectionMultiplexer redis, E_commerceContext context)
        {
            _redis = redis.GetDatabase();
            _context = context;
        }

        public async Task AddItemAsync(string guestId, int productId, int quantity)
        {
            var cart = await GetCartAsync(guestId) ?? new Dictionary<int, int>();

            // Check if product exists in DB to prevent adding invalid items
            var product = await _context.Product.FindAsync(productId);
            if (product == null)
            {
                return; // Or throw exception
            }

            if (cart.ContainsKey(productId))
            {
                int newQty = cart[productId] + quantity;
                cart[productId] = (int)Math.Min(newQty, product.Available_Qty);
            }
            else
            {
                cart[productId] = (int)Math.Min(quantity, product.Available_Qty);
            }
            
            await SaveCartAsync(guestId, cart);
        }

        public async Task<Dictionary<int, int>?> GetCartAsync(string guestId)
        {
             var cartJson = await _redis.StringGetAsync(guestId);
            if (!cartJson.HasValue)
                return null;

            return JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson!);
        }

        private async Task SaveCartAsync(string guestId, Dictionary<int, int> cart)
        {
            await _redis.StringSetAsync(
                guestId,
                JsonSerializer.Serialize(cart),
                TimeSpan.FromHours(CartTtlHours));
        }
    }
}
