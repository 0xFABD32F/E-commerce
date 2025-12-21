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
        public DbSet<E_commerce.Models.Cart> Cart { get; set; } = default!;        
        
        public DbSet<E_commerce.Models.productLine> ProductLine { get; set; } = default!;
        public DbSet<E_commerce.Models.Category> Category { get; set; } = default!;
        public DbSet<E_commerce.Models.User> User { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();
            modelBuilder.Entity<Cart>()
                .HasMany(c => c.productLines)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username)
                      .IsUnique();

                entity.HasIndex(u => u.Email)
                      .IsUnique();
            });

        }

    }
}
