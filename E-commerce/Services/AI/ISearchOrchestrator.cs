using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    public interface ISearchOrchestrator
    {
        /// <summary>
        /// Processes a user query using Multi-Query RAG pipeline.
        /// </summary>
        /// <param name="userQuery">The raw user question.</param>
        /// <returns>Final AI answer.</returns>
        Task<string> GetAnswerAsync(string userQuery);
    }
}
