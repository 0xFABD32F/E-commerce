using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;


namespace E_commerce.Pages
{
    public class CartModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IDatabase _redis;

        public Cart Cart { get; set; } = new Cart();

        public CartModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        public async Task OnGetAsync()
        {
            if (!Request.Cookies.TryGetValue("GuestId", out var guestId))
                return;

            var cartJson = await _redis.StringGetAsync(guestId);
            if (!cartJson.HasValue)
                return;

            var cart = JsonSerializer.Deserialize<Cart>(cartJson!)!;
            if (cart.productLines.Count == 0)
                return;

            // 1. Collect product IDs from cart
            var productIds = cart.productLines
                .Select(l => l.ProductId)
                .Distinct()
                .ToList();

            // 2. Load all products in ONE query
            var products = await _context.Product
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            // 3. Attach products or remove invalid lines
            cart.productLines.RemoveAll(line =>
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    return true; // remove line if product no longer exists

                line.Product = product; // attach navigation property
                return false;
            });

            // 4. Save cleaned cart back to Redis
            await _redis.StringSetAsync(
                guestId,
                JsonSerializer.Serialize(cart),
                TimeSpan.FromHours(2));

            Cart = cart;
        }

        public async Task<IActionResult> OnPostAsync(
    int[] ProductIds,
    uint[] Quantities,
    string? remove,
    string? update)
        {
            // 1. Get guest cart key
            if (!Request.Cookies.TryGetValue("GuestId", out var guestId))
                return RedirectToPage();

            var cartJson = await _redis.StringGetAsync(guestId);
            if (!cartJson.HasValue)
                return RedirectToPage();

            var cart = JsonSerializer.Deserialize<Cart>(cartJson!)!;
            if (cart.productLines.Count == 0)
                return RedirectToPage();

            // 2. REMOVE
            if (!string.IsNullOrWhiteSpace(remove) &&
                int.TryParse(remove, out int removeId))
            {
                cart.productLines.RemoveAll(l => l.ProductId == removeId);
            }

            // 3. UPDATE
            else if (!string.IsNullOrWhiteSpace(update))
            {
                // Defensive check
                if (ProductIds.Length != Quantities.Length)
                    return BadRequest();

                // Load all products once
                var productIds = ProductIds.Distinct().ToList();

                var products = await _context.Product
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                for (int i = 0; i < ProductIds.Length; i++)
                {
                    var line = cart.productLines
                        .FirstOrDefault(l => l.ProductId == ProductIds[i]);

                    if (line == null)
                        continue;

                    // Remove invalid quantities
                    if (Quantities[i] == 0 || !products.ContainsKey(line.ProductId))
                    {
                        cart.productLines.Remove(line);
                        continue;
                    }

                    line.SelectedQty = Quantities[i];
                    line.Product = products[line.ProductId]; // trusted price
                }
            }

            // 4. Save cart back to Redis
            await _redis.StringSetAsync(
                guestId,
                JsonSerializer.Serialize(cart),
                TimeSpan.FromHours(2));

            return RedirectToPage();
        }



    }
}