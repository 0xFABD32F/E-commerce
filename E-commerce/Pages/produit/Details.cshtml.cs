using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.produit
{
    public class DetailsModel : PageModel
    {
        private readonly E_commerceContext _context;

        public DetailsModel(E_commerceContext context)
        {
            _context = context;
        }

        public Product Product { get; private set; } = default!;

        /* =========================
         * Razor Page entry points
         * ========================= */

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await LoadProductAsync(id.Value);
            if (product == null)
                return NotFound();

            Product = product;
            return Page();
        }

        /* =========================
         * Data loading
         * ========================= */

        private async Task<Product?> LoadProductAsync(int id)
        {
            /*
             * AsNoTracking is intentional here because the product
             * is displayed only and never modified.
             * This avoids unnecessary change tracking overhead.
             *
             * Reference:
             * https://learn.microsoft.com/en-us/ef/core/querying/tracking
             */
            return await _context.Product
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}
