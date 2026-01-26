using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_commerce.Services
{
    public interface ICartService
    {
        Task AddItemAsync(string guestId, int productId, int quantity);
        Task<Dictionary<int, int>?> GetCartAsync(string guestId);
    }
}
