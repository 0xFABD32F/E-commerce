using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace E_commerce.Pages
{
    /// <summary>
    /// Manages the shopping cart lifecycle for guest users.
    ///
    /// Design rationale:
    /// - Carts are stored in Redis for fast access and automatic expiration.
    /// - A GuestId cookie is used as the cart key instead of authentication state.
    /// - Product data is revalidated against the database on every request
    ///   to prevent stale or tampered cart data.
    ///
    /// Assumptions:
    /// - A valid GuestId cookie is created earlier in the request pipeline.
    /// - Redis is available and configured as a volatile cache, not a source of truth.
    /// </summary>
    public class CartModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IDatabase _redis;

        /// <summary>
        /// Cart exposed to the Razor view.
        /// Initialized defensively to avoid null checks in the UI layer.
        /// </summary>
        public Cart Cart { get; private set; } = new();

        /// <summary>
        /// Time-to-live for guest carts.
        /// This value balances usability (cart persistence)
        /// with resource cleanup in Redis.
        /// </summary>
        private const int CartTtlHours = 2;

        public CartModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        /* =========================
         * Razor Page entry points
         * ========================= */

        /// <summary>
        /// Loads the cart from Redis, validates its contents,
        /// and synchronizes product data before rendering.
        ///
        /// If no cart exists, the page renders an empty state.
        /// </summary>
        public async Task OnGetAsync()
        {
            var cart = await LoadCartAsync();
            if (cart == null)
                return;

            await AttachProductsAndCleanAsync(cart);
            await SaveCartAsync(cart);

            Cart = cart;
        }

        /// <summary>
        /// Handles cart mutations initiated from the UI.
        ///
        /// The method supports:
        /// - Removing a single product
        /// - Updating quantities in bulk
        ///
        /// All operations are followed by persistence to Redis
        /// to keep the cart state consistent across requests.
        /// </summary>
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

        /// <summary>
        /// Loads the cart associated with the current GuestId.
        ///
        /// Returning null instead of an empty cart allows callers
        /// to distinguish between "no cart yet" and "empty cart".
        /// </summary>
        private async Task<Cart?> LoadCartAsync()
        {
            if (!Request.Cookies.TryGetValue("GuestId", out var guestId))
                return null;

            var cartJson = await _redis.StringGetAsync(guestId);
            if (!cartJson.HasValue)
                return null;

            return JsonSerializer.Deserialize<Cart>(cartJson!);
        }

        /// <summary>
        /// Persists the cart back to Redis and refreshes its TTL.
        ///
        /// Refreshing the expiration on each update prevents
        /// active carts from expiring mid-session.
        /// </summary>
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

        /// <summary>
        /// Synchronizes cart lines with current product data.
        ///
        /// Invalid or deleted products are removed to prevent:
        /// - checkout of non-existent items
        /// - price or stock inconsistencies
        /// </summary>
        private async Task AttachProductsAndCleanAsync(Cart cart)
        {
            if (cart.productLines.Count == 0)
                return;

            var products = await LoadProductsAsync(
                cart.productLines.Select(l => l.ProductId));

            /*
             * RemoveAll is intentionally used here to avoid modifying
             * the collection during enumeration, which would lead to
             * runtime exceptions or subtle logic bugs.
             */
            cart.productLines.RemoveAll(line =>
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    return true;

                line.Product = product;
                return false;
            });
        }

        /// <summary>
        /// Loads products in bulk to minimize database round-trips.
        ///
        /// Returning a dictionary allows O(1) lookups during
        /// cart reconciliation.
        /// </summary>
        private async Task<Dictionary<int, Product>> LoadProductsAsync(IEnumerable<int> ids)
        {
            return await _context.Product
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
        }

        /* =========================
         * Cart mutation logic
         * ========================= */

        /// <summary>
        /// Attempts to remove a cart line based on a string identifier.
        ///
        /// Returning a boolean allows the caller to short-circuit
        /// additional processing when a removal occurs.
        /// </summary>
        private static bool TryHandleRemove(Cart cart, string? remove)
        {
            if (string.IsNullOrWhiteSpace(remove))
                return false;

            if (!int.TryParse(remove, out var productId))
                return false;

            cart.productLines.RemoveAll(l => l.ProductId == productId);
            return true;
        }

        /// <summary>
        /// Updates cart quantities in bulk.
        ///
        /// All input is treated as untrusted, even if it originates
        /// from server-rendered HTML.
        /// </summary>
        private async Task UpdateQuantitiesAsync(
            Cart cart,
            int[] productIds,
            uint[] quantities)
        {
            /*
             * Mismatched arrays indicate a programmer or integration error,
             * not a user mistake. Failing fast here prevents silent data corruption.
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
                    // Product removed after cart creation → cart must self-heal
                    cart.productLines.Remove(line);
                    continue;
                }

                /*
                 * Quantity clamping is enforced server-side to prevent:
                 * - negative quantities
                 * - integer overflows
                 * - stock manipulation
                 *
                 * Client-side validation is advisory only.
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
