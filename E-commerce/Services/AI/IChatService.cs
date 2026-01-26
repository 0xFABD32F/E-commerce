using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    public interface IChatService
    {
        Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt);
    }
}
