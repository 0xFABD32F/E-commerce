using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E_commerce.Pages.produit
{
    //The user must be logged in to do CRUD operations
    //The session cookie must me set in the login page after succesful log in
    //In order to proceed to checkout, the user must be logged in
    public class CreateModel : PageModel
    {
        private readonly E_commerce.Data.E_commerceContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public SelectList Categories { get; set; }

        public CreateModel(E_commerce.Data.E_commerceContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult OnGet()
        {
            Categories = new SelectList(_context.Category, "Id", "Name");
            return Page();
        }

       

        [BindProperty]
        public E_commerce.Models.Product Product { get; set; } = default!;

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
{
            if (!ModelState.IsValid)
            {
                Categories = new SelectList(_context.Category, "Id", "Name");
                return Page();
            }

            bool categoryExists = await _context.Category
                .AnyAsync(c => c.Id == Product.CategoryId);

            if (!categoryExists)
            {
                ModelState.AddModelError("Product.CategoryId",
                    "This category does not exist.");
                Categories = new SelectList(_context.Category, "Id", "Name");
                return Page();
            }

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ImageFile", "Only image files are allowed.");
                    Categories = new SelectList(_context.Category, "Id", "Name");
                    return Page();
                }

                var fileName = Guid.NewGuid() + fileExtension;
                var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images");

                Directory.CreateDirectory(imagePath);

                var filePath = Path.Combine(imagePath, fileName);

                using var fileStream = new FileStream(filePath, FileMode.Create);
                await ImageFile.CopyToAsync(fileStream);

                Product.ImgPath = $"images/{fileName}";
            }    
                _context.Product.Add(Product);
                await _context.SaveChangesAsync();

                return RedirectToPage("./MyListedProducts");
        }

      
    }
}

    
