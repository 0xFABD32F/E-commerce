using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.Marshalling;


namespace E_commerce.Models
{
    //ASP .NET cannot validate NULL/Not-nullable properties in this case.
    //Only the compiler notices the error if we try to assign a null value to a non-nullable property.
    //In this case `required` in the .cshtml file can be bypassed by manipulating the HTML code, allowing the
    //attacker to insert null values.
    //The fix is to include data annotations to validate that in the server-side
    //
    //Sql database has a decimal precision of (18(total number of digits),2(digits after decimal)) meaning that any
    //input that exceeds that will be truncated which violates the data integrity
    //To fix this we can allow a certain range for Product.Price
    //
    //Because different DB providers have different defaults for decimal, and to allow cross-data consistency in
    // the future for different DBMS, we explicitly specify the Precision.
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Price is required.")]        
        [Range(0.01, 1000000000, ErrorMessage = "Price must be greater than zero.")]
        [Precision(18, 2)]
        public decimal Price { get; set; }
        public string? ImgPath { get; set; }

        //[Required(ErrorMessage = "Category is required")]
        //public int CategoryId { get; set; }
        
        //public Category Category { get; set; }
        [Required(ErrorMessage ="Available quantity is required")]
        public uint Available_Qty {  get; set; }

        public bool IsAvailable(uint Qty)
        {
            return (Available_Qty >= Qty);

        }
    }
}
