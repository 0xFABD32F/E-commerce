using E_commerce.Data;
using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace E_commerce.Pages.user
{
    public class DetailsModel : PageModel
    {
        private readonly E_commerce.Data.E_commerceContext _context;

        public DetailsModel(E_commerce.Data.E_commerceContext context)
        {
            _context = context;
        }

        public User user { get; set; } = default!;

        public async Task OnGetAsync()
    {
        // Extract the user's ID from claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) 
                          ?? User.FindFirst(JwtRegisteredClaimNames.Sub);

        if (userIdClaim == null)
        {
            // Not logged in
            RedirectToPage("/Login/Login");
            return;
        }

        int userId = int.Parse(userIdClaim.Value);

        // Load user from DB
        user = await _context.User.FindAsync(userId);
    }
    }
}
