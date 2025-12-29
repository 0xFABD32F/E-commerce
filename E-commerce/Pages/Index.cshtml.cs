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
    public class IndexModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IDatabase _redis;

        private const int GuestCookieLifetimeDays = 2;

        public IndexModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        //Used for the Cart cache value
        [BindProperty]
        public int Id { get; set; }

        [BindProperty]
        public int ProductId { get; set; }
        //the Dictionary stores the product's Id with its quantity
        public Dictionary<int, int>? CartProducts;

        //Add a CartPreviewDTO          IList<CartPreviewDTO> cart{get, set} = new List<CartPreviewDTO>;
        public IList<Product> Product { get; private set; } = new List<Product>();
        public IList<Category> Categories { get; private set; } = new List<Category>();

        /* =========================
         * Razor Page entry points
         * ========================= */

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

        private void EnsureGuestCookie()
        {
            if (Request.Cookies.ContainsKey("GuestId"))
                return;

            /*
             * The GuestId is a server-generated identifier used exclusively
             * for cart tracking. It is intentionally opaque and non-semantic.
             *
             * Reference:
             * https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html
             */
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
            return await _context.Category.ToListAsync();
        }

        private async Task<IList<Product>> LoadFilteredProductsAsync(
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string? stockStatus)
        {
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
            /*
             * Stock filtering is case-insensitive but intentionally explicit.
             * Using magic strings here is acceptable because values are UI-bound
             * and not reused elsewhere.
             */
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

        private async Task<Dictionary<int ,int>> LoadCartAsync(string guestId)
        {
            var cartJson = await _redis.StringGetAsync(guestId);

            return cartJson.HasValue
                ? JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson!)!
                : CartProducts = new Dictionary<int, int>();
        }       

        private void AddProductToCart(CartDTO dto)
        {
            if (CartProducts.ContainsKey(dto.Id))
            {
                CartProducts[dto.Id]++;
            }
            else
            {
                CartProducts.Add(dto.Id, dto.Qty);
            }
            
        }

        private async Task SaveCartAsync(string guestId, Dictionary<int, int> cart)
        {
            await _redis.StringSetAsync(
                guestId,
                JsonSerializer.Serialize(cart));
        }

        //DTO Handler       
        public async Task<IActionResult> OnPostCachePreviewAsync(
            [FromBody] ProductPreviewDTO dto)
        {
            //If key already exists, then overwrite value and reset TTL (To keep cache fresh)
            await _redis.StringSetAsync(
                $"ProductPreview:{dto.Id}",
                JsonSerializer.Serialize(dto),
                TimeSpan.FromMinutes(2)
            );

            return new EmptyResult();
        }



    }
}
