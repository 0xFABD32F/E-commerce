using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_commerce.Data.Services
{
    public interface IAIService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<string> ChatAsync(string userMessage);
        Task IndexProductAsync(E_commerce.Models.Product product);
    }
}
