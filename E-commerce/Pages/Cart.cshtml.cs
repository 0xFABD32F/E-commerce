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

        public Cart Cart { get; private set; } = new();

        private const int CartTtlHours = 2;

        public CartModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        /* =========================
         * Razor Page entry points
         * ========================= */

        public async Task OnGetAsync()
        {
            var cart = await LoadCartAsync();
            if (cart == null)
                return;

            await AttachProductsAndCleanAsync(cart);
            await SaveCartAsync(cart);

            Cart = cart;
        }

        public async Task<IActionResult> OnPostAsync(
            int[] ProductIds,
            uint[] Quantities,
            string? remove,
            string? update)
        {
            var cart = await LoadCartAsync();
            if (cart == null)
                return RedirectToPage();

            if (TryHandleRemove(cart, remove))
            {
                await SaveCartAsync(cart);
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(update))
            {
                await UpdateQuantitiesAsync(cart, ProductIds, Quantities);
                await SaveCartAsync(cart);
            }

            return RedirectToPage();
        }

        /* =========================
         * Cart loading / persistence
         * ========================= */

        private async Task<Cart?> LoadCartAsync()
        {
            if (!Request.Cookies.TryGetValue("GuestId", out var guestId))
                return null;

            var cartJson = await _redis.StringGetAsync(guestId);
            if (!cartJson.HasValue)
                return null;

            return JsonSerializer.Deserialize<Cart>(cartJson!);
        }

        private async Task SaveCartAsync(Cart cart)
        {
            var guestId = Request.Cookies["GuestId"]!;

            await _redis.StringSetAsync(
                guestId,
                JsonSerializer.Serialize(cart),
                TimeSpan.FromHours(CartTtlHours));
        }

        /* =========================
         * Cart consistency logic
         * ========================= */

        private async Task AttachProductsAndCleanAsync(Cart cart)
        {
            if (cart.productLines.Count == 0)
                return;

            var products = await LoadProductsAsync(
                cart.productLines.Select(l => l.ProductId));

            /*
             * This removes invalid cart lines while iterating safely.
             * Using RemoveAll avoids mutation bugs caused by foreach removal.
             */
            cart.productLines.RemoveAll(line =>
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    return true;

                line.Product = product;
                return false;
            });
        }

        private async Task<Dictionary<int, Product>> LoadProductsAsync(IEnumerable<int> ids)
        {
            return await _context.Product
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
        }

        /* =========================
         * Cart mutation logic
         * ========================= */

        private static bool TryHandleRemove(Cart cart, string? remove)
        {
            if (string.IsNullOrWhiteSpace(remove))
                return false;

            if (!int.TryParse(remove, out var productId))
                return false;

            cart.productLines.RemoveAll(l => l.ProductId == productId);
            return true;
        }

        private async Task UpdateQuantitiesAsync(
            Cart cart,
            int[] productIds,
            uint[] quantities)
        {
            /*
             * Mismatched arrays indicate a malformed request.
             * This is treated as a programmer error, not user input.
             */
            if (productIds.Length != quantities.Length)
                throw new InvalidOperationException("ProductIds and Quantities length mismatch.");

            var products = await LoadProductsAsync(productIds);

            for (int i = 0; i < productIds.Length; i++)
            {
                var line = cart.productLines
                    .FirstOrDefault(l => l.ProductId == productIds[i]);

                if (line == null)
                    continue;

                if (!products.TryGetValue(line.ProductId, out var product))
                {
                    // Product deleted after cart creation → remove line
                    cart.productLines.Remove(line);
                    continue;
                }

                /*
                 * Quantity is clamped server-side to prevent:
                 * - negative values
                 * - stock manipulation
                 *
                 * Client-side validation is never trusted.
                 * Reference:
                 * https://owasp.org/www-community/attacks/Parameter_tampering
                 */
                if (quantities[i] == 0)
                {
                    cart.productLines.Remove(line);
                    continue;
                }

                line.SelectedQty = Math.Min(quantities[i], product.Available_Qty);
                line.Product = product;
            }
        }
    }
}
