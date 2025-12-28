using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.login
{
    /// <summary>
    /// Handles user registration by validating input,
    /// creating a persisted User entity, and enforcing uniqueness constraints.
    ///
    /// Assumptions:
    /// - Database enforces UNIQUE constraints on Username and Email.
    /// - Password hashing is required before persistence.
    /// - This page is accessible only to unauthenticated users.
    /// </summary>
    public class CreateModel : PageModel
    {
        private readonly E_commerceContext _context;

        public CreateModel(E_commerceContext context)
        {
            _context = context;
        }

        // =========================
        // ViewModel binding
        // =========================

        /// <summary>
        /// Holds raw user input from the registration form.
        /// Binding directly to the User entity is intentionally avoided
        /// to prevent over-posting and accidental persistence of sensitive fields.
        /// </summary>
        [BindProperty]
        public User Input { get; set; } = new();

        // =========================
        // POST
        // =========================

        /// <summary>
        /// Handles account creation.
        /// The method coordinates validation, user construction,
        /// persistence, and error translation.
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = CreateUserEntity(Input);

            var result = await TryPersistUserAsync(user);
            if (!result)
            {
                AddUniquenessError();
                return Page();
            }

            return RedirectToPage("/login");
        }

        // =========================
        // User creation logic
        // =========================

        /// <summary>
        /// Creates a new User entity from validated input.
        ///
        /// Password hashing is performed here to ensure that
        /// plaintext passwords never leave the request boundary.
        /// </summary>
        private static User CreateUserEntity(User input)
        {
            return new User
            {
                Username = input.Username,
                Email = input.Email,
                Address = input.Address,
                City = input.City,
                ZipCode = input.ZipCode,

                // BCrypt is intentionally used due to its adaptive cost
                // and built-in resistance to brute-force attacks.
                // Reference: https://github.com/BcryptNet/bcrypt.net
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.PasswordHash),

                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
        }

        // =========================
        // Persistence logic
        // =========================

        /// <summary>
        /// Attempts to persist a new user.
        ///
        /// Any database constraint violation (e.g., duplicate username/email)
        /// is intentionally caught and translated into a user-friendly message.
        ///
        /// This avoids leaking database structure or constraint details.
        /// </summary>
        private async Task<bool> TryPersistUserAsync(User user)
        {
            try
            {
                _context.User.Add(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // A DbUpdateException here is expected behavior when
                // UNIQUE constraints are violated.
                //
                // Reference:
                // https://learn.microsoft.com/ef/core/saving/transactions
                return false;
            }
        }

        // =========================
        // Error handling
        // =========================

        /// <summary>
        /// Adds a generic error message for uniqueness violations.
        ///
        /// A single message is used to prevent attackers from
        /// inferring which field (username or email) already exists.
        /// </summary>
        private void AddUniquenessError()
        {
            ModelState.AddModelError(
                string.Empty,
                "Username or Email already exists"
            );
        }
    }
}
