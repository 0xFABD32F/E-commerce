using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks;
using E_commerce.Models;
using Microsoft.Extensions.Logging;

namespace E_commerce.Services.AI
{
    public class VectorStoreSeeder
    {
        private readonly IVectorStore _vectorStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<VectorStoreSeeder> _logger;

        public VectorStoreSeeder(
            IVectorStore vectorStore, 
            IEmbeddingService embeddingService, 
            ILogger<VectorStoreSeeder> logger)
        {
            _vectorStore = vectorStore;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task SeedAsync(List<Product> products)
        {
            try
            {
                await _vectorStore.EnsureIndexExistsAsync();
           
                _logger.LogInformation($"Starting bulk seeding for {products.Count} products...");

                if (products.Count == 0)
                {
                     _logger.LogWarning("No products provided for seeding.");
                     return;
                }

                // 1. Prepare texts for embedding
                var textsToIndex = products.Select(p => 
                    $"Category: {p.Category?.Name ?? "General"}. Price: {p.Price} DH."
                ).ToList();

                // 2. Bulk Generate Embeddings
                var swEmb = System.Diagnostics.Stopwatch.StartNew();
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToIndex);
                swEmb.Stop();
                _logger.LogInformation($"Generated {embeddings.Count} embeddings in {swEmb.ElapsedMilliseconds}ms");

                // 3. Prepare Upsert Items
                var upsertItems = new List<(string id, float[] vector, Dictionary<string, object> metadata)>();

                for (int i = 0; i < products.Count; i++)
                {
                    var product = products[i];
                    var embedding = embeddings[i];

                    if (embedding.Length > 0)
                    {
                        var metadata = new Dictionary<string, object>
                        {
                            { "name", product.Name },
                            { "category", product.Category?.Name ?? "General" },
                            { "description", product.Description ?? "" },
                            { "price", product.Price }
                        };
                        upsertItems.Add((product.Id.ToString(), embedding, metadata));
                    }
                }

                // 4. Bulk Upsert
                var swUpsert = System.Diagnostics.Stopwatch.StartNew();
                await _vectorStore.UpsertRangeAsync(upsertItems);
                swUpsert.Stop();

                _logger.LogInformation($"Successfully upserted {upsertItems.Count} products to Vector Store in {swUpsert.ElapsedMilliseconds}ms.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during vector store seeding.");
            }
        }
    }
}
