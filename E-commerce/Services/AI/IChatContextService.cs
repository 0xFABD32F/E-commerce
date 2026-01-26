using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    public interface IChatContextService
    {
        Task PushProductAsync(string guestId, int productId);
        Task<int?> PopProductAsync(string guestId);
    }
}
