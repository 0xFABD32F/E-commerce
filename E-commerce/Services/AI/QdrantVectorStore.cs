using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace E_commerce.Services.AI
{
    /// <summary>
    /// Qdrant Cloud implementation of IVectorStore.
    /// </summary>
    public class QdrantVectorStore : IVectorStore
    {
        private readonly QdrantClient _client;
        private readonly string _collectionName;
        private const int VectorDimension = 768; // nomic-embed-text dimensions

        public QdrantVectorStore(QdrantClient client, IConfiguration config)
        {
            _client = client;
            _collectionName = config["Qdrant:CollectionName"] ?? "products";
        }

        public async Task EnsureIndexExistsAsync()
        {
            var collections = await _client.ListCollectionsAsync();
            
            if (!collections.Contains(_collectionName))
            {
                await _client.CreateCollectionAsync(
                    collectionName: _collectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = VectorDimension,
                        Distance = Distance.Cosine
                    }
                );
            }

            // Ensure payload index for filtering
            await _client.CreatePayloadIndexAsync(
                collectionName: _collectionName,
                fieldName: "category",
                schemaType: PayloadSchemaType.Keyword
            );
        }

        public async Task<bool> IsEmptyAsync()
        {
            try
            {
                var info = await _client.GetCollectionInfoAsync(_collectionName);
                return info.PointsCount == 0;
            }
            catch
            {
                return true;
            }
        }

        public async Task UpsertAsync(string id, float[] vector, Dictionary<string, object> metadata)
        {
            // Parse product ID as numeric for Qdrant point ID
            var numericId = ulong.Parse(id);
            
            var point = new PointStruct
            {
                Id = new PointId { Num = numericId },
                Vectors = vector,
                Payload = 
                {
                    ["name"] = metadata.GetValueOrDefault("name", "")?.ToString() ?? "",
                    ["description"] = metadata.GetValueOrDefault("description", "")?.ToString() ?? "",
                    ["category"] = metadata.GetValueOrDefault("category", "")?.ToString() ?? "",
                    ["price"] = Convert.ToDouble(metadata.GetValueOrDefault("price", 0m))
                }
            };

            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: new List<PointStruct> { point }
            );
        }

        public async Task UpsertRangeAsync(List<(string id, float[] vector, Dictionary<string, object> metadata)> items)
        {
             var points = new List<PointStruct>();

             foreach (var item in items)
             {
                var numericId = ulong.Parse(item.id);
                
                var point = new PointStruct
                {
                    Id = new PointId { Num = numericId },
                    Vectors = item.vector,
                    Payload = 
                    {
                        ["name"] = item.metadata.GetValueOrDefault("name", "")?.ToString() ?? "",
                        ["description"] = item.metadata.GetValueOrDefault("description", "")?.ToString() ?? "",
                        ["category"] = item.metadata.GetValueOrDefault("category", "")?.ToString() ?? "",
                        ["price"] = Convert.ToDouble(item.metadata.GetValueOrDefault("price", 0m))
                    }
                };
                points.Add(point);
             }

             if (points.Count > 0)
             {
                await _client.UpsertAsync(
                    collectionName: _collectionName,
                    points: points
                );
             }
        }

        public async Task<List<SearchResult>> SearchAsync(float[] queryVector, int limit = 3, string? categoryFilter = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            Filter? filter = null;
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                filter = new Filter
                {
                    Must = 
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "category",
                                Match = new Match { Keyword = categoryFilter } 
                            }
                        }
                    }
                };
            }

            var results = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryVector,
                limit: (ulong)limit,
                filter: filter,
                payloadSelector: true
            );
            
            sw.Stop();
            Console.WriteLine($"[PERF] QdrantVectorStore SearchAsync took {sw.ElapsedMilliseconds}ms. Documents found: {results.Count}. Filter: {categoryFilter ?? "None"}");

            var output = new List<SearchResult>();

            foreach (var result in results)
            {
                var name = result.Payload.TryGetValue("name", out var n) ? n.StringValue : "Unknown Product";
                var desc = result.Payload.TryGetValue("description", out var d) ? d.StringValue : "";
                var category = result.Payload.TryGetValue("category", out var c) ? c.StringValue : "";
                var price = result.Payload.TryGetValue("price", out var p) ? p.DoubleValue : 0;

                output.Add(new SearchResult
                {
                    Id = result.Id.Uuid,
                    Score = result.Score,
                    Content = $"Product: {name}. Category: {category}. Price: {price} DH. Description: {desc}"
                });
            }

            return output;
        }
    }
}
