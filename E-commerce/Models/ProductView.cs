namespace E_commerce.Models
{
    public class ProductView
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public uint Available_Qty { get; set; }


        public decimal CalculateTotal(Dictionary<int,ProductView> ProductInfo, Dictionary<int, int> ProductCache)
        {
            decimal total = 0;
            foreach (var (id, product) in ProductInfo) {
                total += product.Price * ProductCache[id];
            }
            return total;
        }
    }
}
