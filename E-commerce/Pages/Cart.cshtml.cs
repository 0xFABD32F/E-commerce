using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
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
        //public Cart Cart { get; private set; } = new();
        //Product product
        

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
        public ProductView ProductView { get; set; } = new();

        public Dictionary<int,ProductView>? ProductInfo ;
        public Dictionary<int, int>? ProductCache;

        public async Task OnGetAsync()
        {
            
            var cart = await LoadCartAsync();
            if (cart == null)
                return;

            await AttachProductsAndCleanAsync(cart);
            await SaveCartAsync(cart);

            ProductCache = cart;


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
        private async Task<Dictionary<int, int>?> LoadCartAsync()
        {
            if (!Request.Cookies.TryGetValue("GuestId", out var guestId))
                return null;

            var cartJson = await _redis.StringGetAsync(guestId);
            if (!cartJson.HasValue)
                return null;

            return JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson!);
        }

        /// <summary>
        /// Persists the cart back to Redis and refreshes its TTL.
        ///
        /// Refreshing the expiration on each update prevents
        /// active carts from expiring mid-session.
        /// </summary>
        private async Task SaveCartAsync(Dictionary<int, int> cart)
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
        private async Task AttachProductsAndCleanAsync(Dictionary<int, int> cart)
        {
            if (cart == null || cart.Count == 0)
                return;

            var products = await LoadProductsAsync(cart.Keys);

            // Collect keys to remove (products that don't exist)
            var keysToRemove = cart.Keys
                .Where(productId => !products.ContainsKey(productId))
                .ToList();

            // Remove invalid products from cart
            foreach (var key in keysToRemove)
            {
                cart.Remove(key);
            }
        }

        /// <summary>
        /// Loads products in bulk to minimize database round-trips.
        ///
        /// Returning a dictionary allows O(1) lookups during
        /// cart reconciliation.
        /// </summary>
        private async Task<Dictionary<int, ProductView>> LoadProductsAsync(IEnumerable<int> ids)
        {
            ProductInfo = await _context.Product
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(
                    p => p.Id,
                    p => new ProductView
                    {
                        Name = p.Name,
                        Price = p.Price,
                        Available_Qty = p.Available_Qty
                    });

            return ProductInfo;
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
        private static bool TryHandleRemove(Dictionary<int, int> cart, string? remove)
        {
            if (string.IsNullOrWhiteSpace(remove))
                return false;
            if (!int.TryParse(remove, out var productId))
                return false;
            if (!cart.ContainsKey(productId))
                return false;
            cart.Remove(productId);      

            return true;
        }

        /// <summary>
        /// Updates cart quantities in bulk.
        ///
        /// All input is treated as untrusted, even if it originates
        /// from server-rendered HTML.
        /// </summary>
        private async Task UpdateQuantitiesAsync(
            Dictionary<int, int> cart,
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
                var productId = productIds[i];

                if (!cart.ContainsKey(productId))
                    continue;

                if (!products.TryGetValue(productId, out var product))
                {
                    // Product removed after cart creation → cart must self-heal
                    cart.Remove(productId);
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
                    cart.Remove(productId);
                    continue;
                }

                cart[productId] = (int)Math.Min(quantities[i], product.Available_Qty);
            }
        }
    }
}
