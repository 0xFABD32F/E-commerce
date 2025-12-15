using Azure.Core;
using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.RulesetToEditorconfig;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        public SelectList Categories { get; set; }
        [BindProperty]
        public Product Product { get; set; } = default!;

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

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
            Categories = new SelectList(_context.Category, "Id", "Name");
            return Page();  // ← Tells framework to render Edit.cshtml
        }
        public async Task<IActionResult> OnPostAsync()
        {
            //ModelState is a dictionary (key - value)
            //keys : names of input fields
            //values : info about the binding, validation errors
            //It tracks:
            //==>What the user submitted
            //==>Validation errors from Data Annotations
            //==>Whether it could be converted to the target type(e.g., string → decimal)
            //In a POST request and after binding the user input to the actual model(which is ModelState),
            //It can communicate with the Razor Pages Helper to show error messages in HTML

            if (!ModelState.IsValid)
                return Page();

            try
            {
                // Load product from DB (EF tracks this)
                var existingProduct = await _context.Product.FindAsync(Product.Id);
                if (existingProduct == null)
                    return NotFound();
                bool exists = await _context.Category
                .AnyAsync(c => c.Name == Product.Category.Name);
                //Checking the submitted category
                if (!exists)
                {
                    ModelState.AddModelError("Product.CategoryId",
                        "This category doesn't exist.");
                    return Page();
                }

                // Update scalar properties
                existingProduct.Name = Product.Name;
                existingProduct.Price = Product.Price;
                existingProduct.Description = Product.Description;
                existingProduct.Available_Qty = Product.Available_Qty;
                existingProduct.CategoryId = Product.CategoryId;

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Check file extension
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImageFile", "Only image files are allowed.");
                        return Page();
                    }

                    // Delete old image
                    if (!string.IsNullOrEmpty(existingProduct.ImgPath))
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, existingProduct.ImgPath);
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }

                    // Save new image
                    var fileName = Guid.NewGuid() + fileExtension;
                    var imagesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

                    if (!Directory.Exists(imagesFolder))
                        Directory.CreateDirectory(imagesFolder);

                    var newImagePath = Path.Combine(imagesFolder, fileName);

                    using (var fileStream = new FileStream(newImagePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    // Save relative path in DB
                    existingProduct.ImgPath = $"images/{fileName}";
                }             
                
                // Save changes in DB
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(Product.Id))
                    return NotFound();
                else
                    throw;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("ImageFile", $"Error uploading file: {ex.Message}");
                return Page();
            }

            return RedirectToPage("./MyListedProducts");
        }

        private bool ProductExists(int id)
        {
            return _context.Product.Any(e => e.Id == id);
        }
    }
}
