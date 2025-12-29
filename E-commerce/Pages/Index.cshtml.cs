using E_commerce.Data;
using E_commerce.Models;
using E_commerce.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Collections.Generic;

namespace E_commerce.Pages
{
    /// <summary>
    /// Handles the main shop page:
    /// - Displays products and categories
    /// - Supports filtering by category, price, and stock
    /// - Provides cart operations for guest users
    /// - Guest users are identified via an opaque "GuestId" cookie.
    /// - Redis is available for cart and cache storage.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IDatabase _redis;

        // Guest cart cookies expire after a short period to limit stale data
        private const int GuestCookieLifetimeDays = 2;

        /// <summary>
        /// Initializes dependencies for database access and Redis caching.
        /// </summary>
        public IndexModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }
        /// <summary>
        /// 
        /// </summary>

        /* =========================
         * Page-bound properties
         * ========================= */

        // Used for binding cart additions
        [BindProperty]
        public int Id { get; set; }

        [BindProperty]
        public int ProductId { get; set; }       
        /*
         * ProductId => Quantity mapping
         * It is used each time the user clicks on "Add to Cart" (Adds product Id => Qty = 1)         * 
         * 
         */
        public Dictionary<int, int>? CartProducts;

        // Holds the list of products and categories (from DB) to render in the UI 
        public IList<Product> Product { get; private set; } = new List<Product>();
        public IList<Category> Categories { get; private set; } = new List<Category>();

        /* =========================
         * Razor Page entry points
         * ========================= */

        /// <summary>
        /// Handles GET requests for the shop page.
        /// Loads categories, filtered products, and ensures the guest cookie exists.
        /// </summary>
        public async Task OnGetAsync(
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string? stockStatus)
        {
            EnsureGuestCookie();

            Categories = await LoadCategoriesAsync();

            Product = await LoadFilteredProductsAsync(
                categoryId,
                minPrice,
                maxPrice,
                stockStatus);
        }

        /// <summary>
        /// Adds a product to the guest user's cart.
        /// </summary>
        public async Task<IActionResult> OnPostAddToCartAsync(CartDTO dto)
        {
            var guestId = GetGuestId();
            if (guestId == null)
                return RedirectToPage();

            var cart = await LoadCartAsync(guestId);
            CartProducts = cart;

            AddProductToCart(dto);

            await SaveCartAsync(guestId, cart);

            return RedirectToPage();
        }

        /* =========================
         * Guest identification
         * ========================= */

        /// <summary>
        /// Ensures a GuestId cookie exists for cart tracking.
        /// Creates a new opaque, HttpOnly, secure cookie if missing.
        /// </summary>
        private void EnsureGuestCookie()
        {
            if (Request.Cookies.ContainsKey("GuestId"))
                return;

            Response.Cookies.Append(
                "GuestId",
                Guid.NewGuid().ToString(),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(GuestCookieLifetimeDays)
                });
        }

        /// <summary>
        /// Returns the current GuestId from cookies, or null if missing.
        /// </summary>
        private string? GetGuestId()
        {
            return Request.Cookies.TryGetValue("GuestId", out var id)
                ? id
                : null;
        }

        /* =========================
         * Product loading & filtering
         * ========================= */

        private async Task<IList<Category>> LoadCategoriesAsync()
        {
            // Loads all categories for filtering and UI rendering
            return await _context.Category.ToListAsync();
        }

        private async Task<IList<Product>> LoadFilteredProductsAsync(
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string? stockStatus)
        {
            // Base query includes category info for display
            var query = _context.Product
                .Include(p => p.Category)
                .AsQueryable();

            query = ApplyCategoryFilter(query, categoryId);
            query = ApplyPriceFilter(query, minPrice, maxPrice);
            query = ApplyStockFilter(query, stockStatus);

            return await query.ToListAsync();
        }

        private static IQueryable<Product> ApplyCategoryFilter(
            IQueryable<Product> query,
            int? categoryId)
        {
            // Only filter if a valid categoryId is provided
            return categoryId.HasValue && categoryId > 0
                ? query.Where(p => p.CategoryId == categoryId)
                : query;
        }

        private static IQueryable<Product> ApplyPriceFilter(
            IQueryable<Product> query,
            decimal? minPrice,
            decimal? maxPrice)
        {
            if (minPrice.HasValue && minPrice > 0)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue && maxPrice > 0)
                query = query.Where(p => p.Price <= maxPrice.Value);

            return query;
        }

        private static IQueryable<Product> ApplyStockFilter(
            IQueryable<Product> query,
            string? stockStatus)
        {            
            return stockStatus?.ToLower() switch
            {
                "instock" => query.Where(p => p.Available_Qty > 0),
                "outofstock" => query.Where(p => p.Available_Qty == 0),
                _ => query
            };
        }

        /* =========================
         * Cart operations
         * ========================= */

        /// <summary>
        /// Loads the cart from Redis for a given GuestId.
        /// Returns a new empty dictionary if none exists.
        /// 
        /// </summary>
        private async Task<Dictionary<int, int>> LoadCartAsync(string guestId)
        {
            var cartJson = await _redis.StringGetAsync(guestId);

            return cartJson.HasValue
                ? JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson!)!
                : CartProducts = new Dictionary<int, int>();
        }

        /// <summary>
        /// Adds or increments a product in the cart.  
        /// </summary>
        private void AddProductToCart(CartDTO dto)
        {
            if (CartProducts == null)
                CartProducts = new Dictionary<int, int>();

            if (CartProducts.TryGetValue(dto.Id, out var existingQty))
            {
                CartProducts[dto.Id] = existingQty + 1;
            }
            else
            {
                CartProducts.Add(dto.Id, dto.Qty);
            }
        }

        /// <summary>
        /// Persists the updated cart back to Redis.
        /// </summary>
        private async Task SaveCartAsync(string guestId, Dictionary<int, int> cart)
        {
            await _redis.StringSetAsync(
                guestId,
                JsonSerializer.Serialize(cart));
        }

        /* =========================
         * DTO Handler
         * ========================= */

        /// <summary>
        /// Caches product previews for short-lived UI updates.
        /// TTL ensures cache freshness and limits stale data exposure.
        /// ///////////////
        /// How it works?
        /// /////////////
        /// The index.cshtml page sends a custom POST request that contains
        /// DTO object holding attributes like product's name, price,
        /// description and imagePath to this handler.
        /// This approach serves to communicate data between two pages without
        /// depending on each other (loose coupling) and to avoid requesting
        /// the DB for the same info that was available on the first page.
        /// 
        /// </summary>
        public async Task<IActionResult> OnPostCachePreviewAsync(
            [FromBody] ProductPreviewDTO dto)
        {
            await _redis.StringSetAsync(
                $"ProductPreview:{dto.Id}",
                JsonSerializer.Serialize(dto),
                TimeSpan.FromMinutes(2) // short-lived cache
            );

            return new EmptyResult();
        }
    }
}
