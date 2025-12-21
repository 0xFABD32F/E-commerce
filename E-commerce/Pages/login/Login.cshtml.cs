using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace E_commerce.Pages.login
{
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
        // ViewModel (ONLY login data)
        // =========================
        [BindProperty]
        public LoginViewModel Input { get; set; } = new();

        // =========================
        // GET
        // =========================
        public IActionResult OnGet()
        {
            //Verify if the token already exists
            if (Request.Cookies.TryGetValue("jwt", out var token))
            {
                return RedirectToPage("../Index");
                
            }
            return Page();
        }

        // =========================
        // POST
        // =========================
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Find user by username
            var user = await _context.User
                .SingleOrDefaultAsync(u => u.Username == Input.Username);

            if (user == null ||
                !BCrypt.Net.BCrypt.Verify(Input.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return Page();
            }

            // Generate JWT
            var token = _jwtService.GenerateToken(user);

            // Store JWT in secure HTTP-only cookie
            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,          // HTTPS only
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddMinutes(60)
            });

            return RedirectToPage("../Index");
        }
    }
}