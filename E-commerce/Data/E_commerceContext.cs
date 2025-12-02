using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using E_commerce.Models;

namespace E_commerce.Data
{
    public class E_commerceContext : DbContext
    {
        public E_commerceContext (DbContextOptions<E_commerceContext> options)
            : base(options)
        {
        }

        public DbSet<E_commerce.Models.Product> Product { get; set; } = default!;
    }
}
