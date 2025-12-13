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

        public async Task OnGetAsync()
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
            //List all the available products
            Product = await _context.Product.ToListAsync();
        }

        public async Task<IActionResult> OnPostAddToCartAsync()
        {
            var guestId = Request.Cookies["GuestId"];
            if (guestId == null)
                return RedirectToPage();

            // Load product from DB
            var product = await _context.Product.FindAsync(ProductId);
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
            //var line = cart.productLines.FirstOrDefault(l => l.ProductId == ProductId);
            var line = new productLine { ProductId = ProductId, SelectedQty = Quantity, Product = product };
            cart.AddToCart(line);     

            // Save cart to Redis
            await _redis.StringSetAsync(guestId, JsonSerializer.Serialize(cart));
            return RedirectToPage("Cart");
        }
    }
}
