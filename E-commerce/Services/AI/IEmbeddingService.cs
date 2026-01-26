using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<List<float[]>> GenerateEmbeddingsAsync(IList<string> texts);
    }
}
