using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace E_commerce.Pages.login
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnPost()
        {
            // Remove the JWT cookie by setting it to expire in the past
            Response.Cookies.Append("jwt", "", new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });           

            return RedirectToPage("/Index");
        }
    }

}
