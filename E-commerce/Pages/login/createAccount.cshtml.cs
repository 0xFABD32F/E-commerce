using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace E_commerce.Pages.login
{
    public class CreateModel : PageModel
    {
        private readonly E_commerceContext _context;

        public CreateModel(E_commerceContext context)
        {
            _context = context;
        }

        [BindProperty]
        public User Input { get; set; } = new();
  

        // =========================
        // POST
        // =========================
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Create User entity
            var user = new User
            {
                Username = Input.Username,
                Email = Input.Email,
                Address = Input.Address,
                City = Input.City,
                ZipCode = Input.ZipCode,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.PasswordHash),
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            try
            {
                _context.User.Add(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "Username or Email already exists");
                return Page();
            }

            return RedirectToPage("/Login");
        }
    }
}