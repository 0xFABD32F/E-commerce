using Microsoft.EntityFrameworkCore;
namespace E_commerce.Models
{
    public class Cart
    {
        public int Id { get; set; }
        [Precision(18, 2)]
        public decimal SubTotal {  get; set; }
        public List<productLine> productLines { get; set; } = new List<productLine>();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;       //After payment is done, We save Cart to database
        public decimal CalculateSubTotal() => productLines.Sum(pl => pl.LineTotal());
       
        public void AddToCart(productLine productLine)
        {
            // Check if product already exists in cart
            var existingLine = productLines
                .FirstOrDefault(pl => pl.ProductId == productLine.ProductId);

            if (existingLine != null)
            {
                // Update quantity
                existingLine.SelectedQty += productLine.SelectedQty;

                // Update subtotal line
                //existingLine.LineTotal = existingLine.SelectedQty * existingLine.Product.Price;
            }
            else
            {
                // Add new product line
                productLines.Add(productLine);
            }
        }
        public void RemoveFromCart(productLine ProductLine) {            
            
            var existingLine = productLines
                .FirstOrDefault(pl => pl.ProductId == ProductLine.ProductId);
            if (existingLine != null) {
                if(existingLine.SelectedQty <= 1)
                {
                    productLines.Remove(existingLine);
                    
                }
                else
                {
                    existingLine.SelectedQty--;

                }
                


            }  


        }
     
    }
}
