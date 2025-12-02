using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace E_commerce.Pages.produit
{
    public class CreateModel : PageModel
    {
        private readonly E_commerce.Data.E_commerceContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CreateModel(E_commerce.Data.E_commerceContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public E_commerce.Models.Product Product { get; set; } = default!;

        [BindProperty]
        public IFormFile ImageFile { get; set; }

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (ImageFile != null && ImageFile.Length > 0)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ImageFile", "Only image files are allowed.");
                    return Page();
                }

                // Create unique filename
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                
                // Ensure directory exists
                if (!Directory.Exists(imagePath))
                {
                    Directory.CreateDirectory(imagePath);
                }

                var filePath = Path.Combine(imagePath, fileName);

                // Save file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(fileStream);
                }

                // Store relative path in database
                Product.ImgPath = Path.Combine("images", fileName).Replace("\\", "/");
            }

            _context.Product.Add(Product);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
