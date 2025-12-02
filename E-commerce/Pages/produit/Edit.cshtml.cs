using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace E_commerce.Pages.produit
{
    public class EditModel : PageModel
    {
        private readonly E_commerce.Data.E_commerceContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public EditModel(E_commerce.Data.E_commerceContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public Product Product { get; set; } = default!;

        [BindProperty]
        public IFormFile ImageFile { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Product.FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            Product = product;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Handle image upload if a new image is provided
            if (ImageFile != null && ImageFile.Length > 0)
            {
                try
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImageFile", "Only image files are allowed.");
                        return Page();
                    }

                    // Delete old image if it exists
                    if (!string.IsNullOrEmpty(Product.ImgPath))
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, Product.ImgPath);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Create unique filename for new image
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
                catch (Exception ex)
                {
                    ModelState.AddModelError("ImageFile", $"Error uploading file: {ex.Message}");
                    return Page();
                }
            }

            _context.Attach(Product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(Product.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ProductExists(int id)
        {
            return _context.Product.Any(e => e.Id == id);
        }
    }
}
