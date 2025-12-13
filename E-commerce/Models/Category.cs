using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models
{
    public class Category
    {
        
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
       
    }
}
