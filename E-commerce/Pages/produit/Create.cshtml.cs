using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.produit
{
    public class CreateModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        public CreateModel(
            E_commerceContext context,
            IWebHostEnvironment environment)
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

        public async Task<IActionResult> OnGetAsync()
        {
            Categories = await LoadCategoriesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            /*
             * Validation must complete before any side effects
             * (database writes or file system access).
             */
            if (!ModelState.IsValid)
            {
                Categories = await LoadCategoriesAsync();
                return Page();
            }

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
                if (ImageFile != null)
                    Product.ImgPath = await SaveNewImageAsync(ImageFile);

                _context.Product.Add(Product);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                /*
                 * File system failures are surfaced as validation errors
                 * to avoid leaking infrastructure details.
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
         * File system operations
         * ========================= */

        private async Task<string> SaveNewImageAsync(IFormFile file)
        {
            ValidateImageExtension(file);

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";

            var imagesFolder = Path.Combine(
                _environment.WebRootPath,
                "images");

            if (!Directory.Exists(imagesFolder))
                Directory.CreateDirectory(imagesFolder);

            var fullPath = Path.Combine(imagesFolder, fileName);

            /*
             * The using statement guarantees stream disposal
             * even if the copy operation fails.
             */
            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"images/{fileName}";
        }

        private static void ValidateImageExtension(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    "Only image files are allowed.");
            }
        }
    }
}
