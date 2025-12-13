using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;


namespace E_commerce.Models
{
    public class productLine
    {
        public int Id { get; set; }
        public uint SelectedQty { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Product must be selected.")]
        public int ProductId { get; set; }
        public Product Product {  get; set; }

        public decimal LineTotal()
        {
            return SelectedQty * Product.Price;
        }


    }
}
