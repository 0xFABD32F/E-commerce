using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    /// <summary>
    /// Abstraction for Large Language Model interactions.
    /// Follows Dependency Inversion Principle.
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>
        /// Generates a vector embedding for the given text.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <returns>Float array representing the embedding.</returns>
        Task<float[]> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// Generates a chat completion response.
        /// </summary>
        /// <param name="systemPrompt">System instructions.</param>
        /// <param name="userPrompt">User query.</param>
        /// <returns>AI response string.</returns>
        Task<string> ChatCompletionAsync(string systemPrompt, string userPrompt);
    }
}
