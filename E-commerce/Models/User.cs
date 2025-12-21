using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace E_commerce.Models
{
    [Index(nameof(Username), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        

        public int ID { get; set; }
        [Required(ErrorMessage ="Name is required")]        
        public string Username { get; set; }                //Must be unique
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Password must be at least 8 characters")]
        public string PasswordHash { get; set; }
        [Required(ErrorMessage = "Email is required")]

        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Adress is required")]
        public string Address { get; set; }
        [Required(ErrorMessage = "City is required")]
        public string City { get; set; }
        [Required(ErrorMessage = "Zipcode is required")]
        public string ZipCode { get; set; }        
        public List<Cart>? Orders { get; set; } = new List<Cart>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;       //For account creation
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;      //For account modification


    }
}
