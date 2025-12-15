using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using E_commerce.Data;
using E_commerce.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace E_commerce.Pages
{
    public class IndexModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IDatabase _redis;

        public IndexModel(E_commerceContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        [BindProperty]
        public int ProductId { get; set; }

        [BindProperty]
        public uint Quantity { get; set; }

        public IList<Product> Product { get; set; } = default!;
        public IList<Category> Categories { get; set; } = default!;

        public async Task OnGetAsync(int? categoryId, decimal? minPrice, decimal? maxPrice, string stockStatus)
        {
            //Set guestCookie as a key for Redis database to memorize cart items
            if (!Request.Cookies.ContainsKey("GuestId"))
            {
                var guestId = Guid.NewGuid().ToString(); //Unique key for each guest
                Response.Cookies.Append(
                    "GuestId",
                    guestId,
                    new CookieOptions
                    {
                        HttpOnly = true,           // prevents client-side JS from accessing it
                        Secure = true,             // send only over HTTPS
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays(2) // cookie valid for 30 days
                    }
                );
            }

            // Load all categories for the filter sidebar
            Categories = await _context.Category.ToListAsync();

            // Start with all products, including the Category navigation property
            var query = _context.Product.Include(p => p.Category).AsQueryable();

            // Filter by category if selected
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // Filter by minimum price
            if (minPrice.HasValue && minPrice.Value > 0)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            // Filter by maximum price
            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // Filter by stock status
            if (!string.IsNullOrEmpty(stockStatus))
            {
                if (stockStatus.ToLower() == "instock")
                {
                    query = query.Where(p => p.Available_Qty > 0);
                }
                else if (stockStatus.ToLower() == "outofstock")
                {
                    query = query.Where(p => p.Available_Qty == 0);
                }
            }

            // Execute query and get results
            Product = await query.ToListAsync();
        }

        public async Task<IActionResult> OnPostAddToCartAsync()
        {
            var guestId = Request.Cookies["GuestId"];
            if (guestId == null)
                return RedirectToPage();

            // Load product from DB
            //Added the Available quantity condition to not allow the attacker to add an "Out of Stock" item to the cache if he manipulated the HTML.
            var product = await _context.Product
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == ProductId &&
                    p.Available_Qty > 0
                );

            if (product == null)
                return RedirectToPage();

            // Load or create cart from Redis
            Cart cart;
            var cartJson = await _redis.StringGetAsync(guestId);
            if (cartJson.HasValue)
                cart = JsonSerializer.Deserialize<Cart>(cartJson!)!;
            else
                cart = new Cart();

            // Add or update productLine
            var line = new productLine { ProductId = ProductId, SelectedQty = 1, Product = product };
            cart.AddToCart(line);

            // Save cart to Redis
            await _redis.StringSetAsync(guestId, JsonSerializer.Serialize(cart));

            return RedirectToPage("Cart");
        }
    }
}