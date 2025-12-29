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
    /// Handles all cart-related interactions for guest users.
    ///
    /// Design choice:
    /// - Cart state is stored in Redis for fast access and automatic expiration.
    /// - Products are revalidated on every request to ensure data consistency.
    ///
    /// Assumptions:
    /// - A "GuestId" cookie uniquely identifies an anonymous user session.
    /// - Redis is available and configured as a shared cache.
    /// </summary>
    public class CartModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IDatabase _redis;

        // Cart expiration is intentionally short to limit stale pricing and stock data
        private const int CartTtlHours = 2;
      
        public CartModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        /// <summary>
        /// Used to call the CalculateTotal() method
        /// </summary>
        public ProductView ProductView { get; private set; } = new();

        /// <summary>
        /// Maps ProductId => ProductView
        /// After validating the Cart's cache against the DB, ProductInfo will hold a mix of cached data (Selected_Qty)
        /// and information that comes from DB (Name, Available_Qty, Price) all stored in ProductView object. 
        /// </summary>
        public Dictionary<int, ProductView>? ProductInfo;     

        /// <summary>
        /// Loads the cart, synchronizes it with current product data,
        /// and persists any corrections.
        ///
        /// The cart is cleaned proactively to avoid showing invalid
        /// or deleted products to the user.
        /// </summary>
        public async Task OnGetAsync()
        {
            var cart = await LoadCartAsync();
            if (cart == null)
                return;

            await AttachProductsAndCleanAsync(cart);
            await SaveCartAsync(cart);
            //Separate this
            foreach (var (productId, qty) in cart)
            {
                if (ProductInfo != null && ProductInfo.TryGetValue(productId, out var productView))
                {
                    productView.Selected_Qty = qty;
                }
            }

            //ProductCache = cart;
        }

        /// <summary>
        /// Handles cart mutations (remove or update).
        ///
        /// Explicit branching avoids mixing multiple mutation types
        /// in a single request, reducing side effects.
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

            // Removal takes precedence to avoid conflicting operations
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

        /// <summary>
        /// Loads the cart associated with the current GuestId.
        ///
        /// Returning null (instead of an empty cart) allows callers
        /// to distinguish between:
        /// - a missing session
        /// - an intentionally empty cart
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
        /// Persists the cart to Redis and refreshes its TTL.
        ///
        /// Refreshing expiration on every write ensures that
        /// active users do not lose their cart mid-session.
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
        /// Synchronizes cart entries with current product data.
        ///
        /// Products that no longer exist are removed to prevent:
        /// - checkout of invalid items
        /// - broken UI rendering
        /// - stale stock or pricing issues
        /// </summary>
        private async Task AttachProductsAndCleanAsync(Dictionary<int, int> cart)
        {
            if (cart.Count == 0)
                return;

            var products = await LoadProductsAsync(cart.Keys);

            /*
             * Removal is deferred until after enumeration to avoid
             * modifying the collection while iterating.
             */
            var keysToRemove = cart.Keys
                .Where(productId => !products.ContainsKey(productId))
                .ToList();

            foreach (var key in keysToRemove)
            {
                cart.Remove(key);
            }
        }

        /// <summary>
        /// Loads product data required for cart validation and rendering.
        ///
        /// Only the necessary fields are projected to avoid
        /// over-fetching from the database.
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
        /// Attempts to remove a cart line using a string identifier.
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
        /// All inputs are treated as untrusted, even when generated
        /// by server-rendered HTML, to defend against parameter tampering.
        /// </summary>
        private async Task UpdateQuantitiesAsync(
            Dictionary<int, int> cart,
            int[] productIds,
            uint[] quantities)
        {
            /*
             * A length mismatch indicates a programming or integration error,
             * not a user mistake. Failing fast avoids silent corruption.
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
                    // Product deleted after cart creation => cart must self-heal
                    cart.Remove(productId);
                    continue;
                }

                /*
                 * Quantity clamping is enforced server-side to prevent:
                 * - negative or zero quantities
                 * - integer overflows
                 * - stock manipulation attacks
                 *         
                 */
                if (quantities[i] == 0)
                {
                    cart.Remove(productId);
                    continue;
                }
                //Validating entered quantities against available quantities that came from DB

                cart[productId] = (int)Math.Min(quantities[i], product.Available_Qty);
            }
        }
    }
}
