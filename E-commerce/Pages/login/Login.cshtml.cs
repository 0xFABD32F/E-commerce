using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_commerce.Pages.login
{
    /// <summary>
    /// Handles user authentication using username/password
    /// and establishes an authenticated session via JWT stored in an HttpOnly cookie.
    ///
    /// Assumptions:
    /// - JWT validation middleware is correctly configured.
    /// - The JWT contains a user identifier claim (sub or NameIdentifier).
    /// - HTTPS is enforced in production (required for Secure cookies).
    /// </summary>
    public class LoginModel : PageModel
    {
        private readonly E_commerceContext _context;
        private readonly IJwtService _jwtService;

        public LoginModel(E_commerceContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        // =========================
        // ViewModel
        // =========================

        /// <summary>
        /// Contains only the data required for authentication.
        /// This avoids binding the full User entity, which would
        /// introduce over-posting and validation issues.
        /// </summary>
        [BindProperty]
        public LoginViewModel Input { get; set; } = new();

        // =========================
        // GET
        // =========================

        /// <summary>
        /// Prevents authenticated users from accessing the login page.
        /// The presence of a JWT cookie is treated as an authentication signal;
        /// the token itself will still be validated by middleware on protected pages.
        /// </summary>
        public IActionResult OnGet()
        {
            if (Request.Cookies.TryGetValue("jwt", out _))
            {
                return RedirectToPage("../Index");
            }

            return Page();
        }

        // =========================
        // Authentication logic
        // =========================

        /// <summary>
        /// Authenticates a user using credentials provided by the client.
        ///
        /// Returns null for any failure case to avoid leaking
        /// whether the username or password was incorrect.
        /// </summary>
        private async Task<User?> AuthenticateUserAsync(string username, string password)
        {
            var user = await _context.User
                .SingleOrDefaultAsync(u => u.Username == username);

            if (user is null)
                return null;

            // Password verification is intentionally server-side.
            // Exposing hash comparison logic to the client would
            // create unnecessary attack surface.
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return user;
        }

        /// <summary>
        /// Adds a generic authentication error.
        /// A single message is used to prevent username enumeration attacks.
        /// </summary>
        private void AddInvalidCredentialsError()
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid username or password"
            );
        }

        // =========================
        // Sign-in logic
        // =========================

        /// <summary>
        /// Issues a signed JWT for the authenticated user and
        /// persists it using a secure HttpOnly cookie.
        ///
        /// This method centralizes sign-in behavior so that
        /// future changes (refresh tokens, claims updates)
        /// do not affect the page handler.
        /// </summary>
        private void SignInUser(User user)
        {
            var token = _jwtService.GenerateToken(user);
            AppendJwtCookie(token);
        }

        /// <summary>
        /// Writes the JWT into an HttpOnly cookie.
        /// Cookie-based storage is chosen over localStorage
        /// to mitigate XSS token exfiltration risks.
        /// </summary>
        private void AppendJwtCookie(string token)
        {
            Response.Cookies.Append(
                "jwt",
                token,
                CreateJwtCookieOptions()
            );
        }

        /// <summary>
        /// Centralizes JWT cookie configuration to ensure
        /// consistent security settings across the application.
        ///
        /// Any changes to expiration or SameSite behavior
        /// should be made here to avoid subtle bugs.
        /// </summary>
        private static CookieOptions CreateJwtCookieOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddMinutes(60)

                /*
                 * HttpOnly: prevents JavaScript access (XSS mitigation)
                 * Secure: ensures cookie is sent only over HTTPS
                 * SameSite.Strict: blocks cross-site request contexts (CSRF mitigation)
                 *
                 * Reference:
                 * https://learn.microsoft.com/aspnet/core/security/authentication/cookie
                 */
            };
        }

        // =========================
        // POST
        // =========================

        /// <summary>
        /// Handles login submission.
        /// Validates input, authenticates credentials,
        /// and establishes an authenticated session on success.
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await AuthenticateUserAsync(Input.Username, Input.Password);
            if (user is null)
            {
                AddInvalidCredentialsError();
                return Page();
            }

            SignInUser(user);

            return RedirectToPage("../Index");
        }
    }
}
