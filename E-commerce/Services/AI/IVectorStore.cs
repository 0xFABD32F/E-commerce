using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_commerce.Services.AI
{
    /// <summary>
    /// Abstraction for Vector Database interactions.
    /// Follows Dependency Inversion Principle.
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Ensures the vector index exists. Creates it if not.
        /// </summary>
        Task EnsureIndexExistsAsync();

        /// <summary>
        /// Upserts a product into the vector store.
        /// </summary>
        /// <param name="id">Product ID.</param>
        /// <param name="vector">Embedding vector.</param>
        /// <param name="metadata">Additional metadata (Name, Description, Price).</param>
        Task UpsertAsync(string id, float[] vector, Dictionary<string, object> metadata);
        Task UpsertRangeAsync(List<(string id, float[] vector, Dictionary<string, object> metadata)> items);

        /// <summary>
        /// Performs a K-Nearest Neighbors search.
        /// </summary>
        /// <param name="queryVector">Query embedding.</param>
        /// <param name="limit">Number of results to return.</param>
        /// <param name="categoryFilter">Optional category to filter results.</param>
        /// <returns>List of matching documents (serialized or objects).</returns>
        Task<List<SearchResult>> SearchAsync(float[] queryVector, int limit = 3, string? categoryFilter = null);
        
        /// <summary>
        /// Checks if the store is empty.
        /// </summary>
        Task<bool> IsEmptyAsync();
    }

    public class SearchResult
    {
        public string Id { get; set; }
        public double Score { get; set; }
        public string Content { get; set; }
        // Can add dictionary for metadata if generic access is needed
    }
}
