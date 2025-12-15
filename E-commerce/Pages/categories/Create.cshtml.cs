using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace E_commerce.Pages.categories
{
    public class CreateModel : PageModel
    {
        private readonly E_commerce.Data.E_commerceContext _context;

        public CreateModel(E_commerce.Data.E_commerceContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public E_commerce.Models.Category Category { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            bool exists = await _context.Category
            .AnyAsync(c => c.Name == Category.Name);

            if (exists)
            {
                ModelState.AddModelError("Category.Name",
                    "This category already exists.");
                return Page();
            }

            _context.Category.Add(Category);
            await _context.SaveChangesAsync();

            return RedirectToPage("../Index");
        }
    }
}
