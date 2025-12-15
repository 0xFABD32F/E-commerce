using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.produit
{
    public class DeleteModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IWebHostEnvironment _environment;

        public DeleteModel(
            E_commerceContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [BindProperty]
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

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await LoadTrackedProductAsync(id.Value);
            if (product == null)
                return RedirectToPage("./MyListedProducts");

            try
            {
                DeleteProductImageIfExists(product.ImgPath);

                _context.Product.Remove(product);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                /*
                 * Deletion failures are intentionally not surfaced
                 * to avoid leaking file system or database details.
                 */
                throw;
            }

            return RedirectToPage("./MyListedProducts");
        }

        /* =========================
         * Data loading
         * ========================= */

        private async Task<Product?> LoadProductAsync(int id)
        {
            return await _context.Product
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        private async Task<Product?> LoadTrackedProductAsync(int id)
        {
            /*
             * A tracked entity is required to safely remove it
             * using Entity Framework.
             */
            return await _context.Product.FindAsync(id);
        }

        /* =========================
         * File system operations
         * ========================= */

        private void DeleteProductImageIfExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            /*
             * BUG FIX:
             * The original code attempted to delete the image using
             * a relative path, which fails outside the working directory.
             * The path must be resolved against wwwroot.
             */
            var fullPath = Path.Combine(
                _environment.WebRootPath,
                relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
    }
}
