using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.produit
{
    public class EditModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        public EditModel(E_commerceContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public SelectList Categories { get; private set; } = default!;

        [BindProperty]
        public Product Product { get; set; } = default!;

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

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
            Categories = await LoadCategoriesAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            /*
             * ModelState validation must run before any side effects
             * (database writes or file system access).
             */
            if (!ModelState.IsValid)
            {
                Categories = await LoadCategoriesAsync();
                return Page();
            }

            var existingProduct = await LoadTrackedProductAsync(Product.Id);
            if (existingProduct == null)
                return NotFound();

            if (!await CategoryExistsAsync(Product.CategoryId))
            {
                ModelState.AddModelError(
                    "Product.CategoryId",
                    "This category does not exist.");

                Categories = await LoadCategoriesAsync();
                return Page();
            }

            try
            {
                UpdateScalarFields(existingProduct, Product);

                if (ImageFile != null)
                    await ReplaceProductImageAsync(existingProduct, ImageFile);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                /*
                 * If the product was deleted during editing, the user
                 * should not see a generic concurrency error.
                 */
                if (!ProductExists(Product.Id))
                    return NotFound();

                throw;
            }
            catch (Exception ex)
            {
                /*
                 * File system errors are surfaced as validation errors
                 * to avoid leaking internal paths or stack traces.
                 */
                ModelState.AddModelError(
                    "ImageFile",
                    $"Error uploading file: {ex.Message}");

                Categories = await LoadCategoriesAsync();
                return Page();
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
             * A tracked entity is required here because the edit
             * operation relies on EF change tracking.
             */
            return await _context.Product.FindAsync(id);
        }

        private async Task<SelectList> LoadCategoriesAsync()
        {
            return new SelectList(
                await _context.Category.ToListAsync(),
                "Id",
                "Name");
        }

        private async Task<bool> CategoryExistsAsync(int categoryId)
        {
            return await _context.Category.AnyAsync(c => c.Id == categoryId);
        }

        /* =========================
         * Update logic
         * ========================= */

        private static void UpdateScalarFields(Product target, Product source)
        {
            /*
             * Only explicitly allowed fields are updated.
             * This prevents over-posting vulnerabilities.
             *
             * Reference:
             * https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery
             */
            target.Name = source.Name;
            target.Price = source.Price;
            target.Description = source.Description;
            target.Available_Qty = source.Available_Qty;
            target.CategoryId = source.CategoryId;
        }

        private async Task ReplaceProductImageAsync(
            Product product,
            IFormFile imageFile)
        {
            ValidateImageExtension(imageFile);

            DeleteOldImageIfExists(product.ImgPath);

            var newRelativePath = await SaveNewImageAsync(imageFile);

            product.ImgPath = newRelativePath;
        }

        /* =========================
         * File system operations
         * ========================= */

        private static void ValidateImageExtension(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    "Only image files are allowed.");
            }
        }

        private void DeleteOldImageIfExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        private async Task<string> SaveNewImageAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";

            var imagesFolder = Path.Combine(
                _environment.WebRootPath,
                "images");

            if (!Directory.Exists(imagesFolder))
                Directory.CreateDirectory(imagesFolder);

            var fullPath = Path.Combine(imagesFolder, fileName);

            /*
             * The file stream is wrapped in a using block to ensure
             * proper disposal even if the copy operation fails.
             */
            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"images/{fileName}";
        }

        /* =========================
         * Existence checks
         * ========================= */

        private bool ProductExists(int id)
        {
            return _context.Product.Any(p => p.Id == id);
        }
    }
}
