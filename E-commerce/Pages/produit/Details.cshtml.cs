using E_commerce.Data;
using E_commerce.Models;
using E_commerce.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace E_commerce.Pages.produit
{
    public class DetailsModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IConnectionMultiplexer _redis;
        public DetailsModel(E_commerceContext context,IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis;
        }
        
        
        public ProductPreviewDTO ProductPreview { get; private set; } = new();
  

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await LoadProductAsync(id.Value);
          
            if (product == null)
                return NotFound();

            ProductPreview = product;
            return Page();
        }

        /* =========================
         * Data loading
         * =========================
         */ 
        
        private async Task<ProductPreviewDTO?> LoadProductAsync(int id)

        {
         
            var db = _redis.GetDatabase();
            string cacheKey = $"ProductPreview:{id}"; 
            
            var JsonCachedProduct = await db.StringGetAsync(cacheKey);
            if (!JsonCachedProduct.IsNullOrEmpty)
            {
                return JsonSerializer.Deserialize<ProductPreviewDTO>(JsonCachedProduct);
            }

            var product = await _context.Product
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return null;

            ProductPreview.Id = product.Id;
            ProductPreview.Name = product.Name;
            ProductPreview.Price = product.Price;
            ProductPreview.ImagePath = product.ImgPath;
            ProductPreview.Description = product.Description;

            await db.StringSetAsync(
                cacheKey,
                JsonSerializer.Serialize(ProductPreview),
                TimeSpan.FromMinutes(5) 
            );            

            return ProductPreview;



        }
    }
}
