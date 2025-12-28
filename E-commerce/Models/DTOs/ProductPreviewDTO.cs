namespace E_commerce.Models.DTOs
{
   
     public class ProductPreviewDTO
     {

         public int Id { get; set; }
         public string Name { get; set; } = "";
         public decimal Price { get; set; }
         public string? ImagePath { get; set; } = "";
         public string Description { get; set; } = "";


     }
   
}
