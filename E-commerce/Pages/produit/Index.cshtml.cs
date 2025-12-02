using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using E_commerce.Data;
using E_commerce.Models;

namespace E_commerce.Pages.produit
{
    public class IndexModel : PageModel
    {
        private readonly E_commerce.Data.E_commerceContext _context;

        public IndexModel(E_commerce.Data.E_commerceContext context)
        {
            _context = context;
        }

        public IList<Product> Product { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Product = await _context.Product.ToListAsync();
        }
    }
}
