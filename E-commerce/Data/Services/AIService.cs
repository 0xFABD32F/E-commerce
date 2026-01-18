using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using E_commerce.Models;
using E_commerce.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace E_commerce.Data.Services
{
    public class AIService : IAIService
    {
        private readonly IDatabase _redis;
        private readonly HttpClient _httpClient;
        private readonly string _ollamaBaseUrl;
        private readonly string _embeddingModel;
        private readonly string _chatModel;
        private readonly E_commerceContext _context;

        // Static In-memory cache for vector search (Shared across transient instances)
        private static List<(string Key, float[] Embedding, string Content)> _vectorCache = new();
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10); 
        private static readonly SemaphoreSlim _cacheLock = new(1, 1);

        public AIService(IDatabase redis, HttpClient httpClient, IConfiguration config, E_commerceContext context)
        {
            _redis = redis;
            _httpClient = httpClient;
            _context = context;
            _ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _embeddingModel = config["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
            _chatModel = config["Ollama:ChatModel"] ?? "llama3";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var url = $"{_ollamaBaseUrl}/api/embeddings";
            
            var requestData = new
            {
                model = _embeddingModel,
                prompt = text
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Ollama Embedding Error: {response.StatusCode} - {err}");
                    return Array.Empty<float>();
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseString);
                
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Embedding Error: {ex.Message}");
                return Array.Empty<float>();
            }
        }

        public async Task IndexProductAsync(Product product)
        {
            var textToIndex = $"Product: {product.Name}. Category: {product.Category?.Name}. Price: {product.Price} DH. Description: {product.Description}";
            var embedding = await GenerateEmbeddingAsync(textToIndex);

            if (embedding.Length == 0) return;

            // Store in Redis
            // Note: Redis vector search typically requires fixed dimensions. 
            // nomic-embed-text is 768d. sentence-transformers/all-MiniLM-L6-v2 is 384d.
            // If you have existing data, you might need to re-index or clear the DB.
            await _redis.HashSetAsync($"product:{product.Id}", new HashEntry[]
            {
                new HashEntry("embedding", JsonSerializer.Serialize(embedding)),
                new HashEntry("name", product.Name),
                new HashEntry("description", product.Description ?? ""),
                new HashEntry("price", product.Price.ToString())
            });
        }

        public async Task<string> ChatAsync(string userMessage)
        {
            // 1. Check for greetings/simple queries
            if (IsGreeting(userMessage))
            {
                return "Hello! I am your shopping assistant. How can I help you today?";
            }

            // 2. Retrieval (RAG)
            var queryEmbedding = await GenerateEmbeddingAsync(userMessage);
            var relevantProducts = await SearchSimilarProductsAsync(queryEmbedding);

            // 3. Augmentation & Generation
            if (relevantProducts.Count == 0)
            {
                Console.WriteLine("No relevant products found in storage. Returning fallback.");
                return "I am sorry, but I couldn't find any products in our store matching your description.";
            }

            var systemPrompt = "You are a helpful shopping assistant for a music store. " +
                               " STRICT RULE: You must ONLY use the provided Context to answer. " +
                               " Do not use your own knowledge. " +
                               " If the answer is not in the context, say exactly: 'I am sorry, I cannot find that product in our store.'";
            
            var userPromptWithContext = $"Context:\n{string.Join("\n", relevantProducts)}\n\nUser Question: {userMessage}";
            
            Console.WriteLine($"Sending prompt with {relevantProducts.Count} context items.");
            var response = await CallOllamaChatAsync(systemPrompt, userPromptWithContext);

            return response;
        }

        private bool IsGreeting(string text)
        {
            var greetings = new[] { "hello", "hi", "hey", "good morning", "good evening" };
            return greetings.Any(g => text.Trim().ToLower().StartsWith(g));
        }

        private async Task EnsureCacheLoadedAsync()
        {
            // Optimistic check
            if (_vectorCache.Count > 0 && (DateTime.UtcNow - _lastCacheUpdate) < _cacheDuration)
                return;

            await _cacheLock.WaitAsync();
            try
            {
                // Double-check after lock
                if (_vectorCache.Count > 0 && (DateTime.UtcNow - _lastCacheUpdate) < _cacheDuration)
                    return;

                Console.WriteLine("Reloading vector cache from Redis...");
                var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: "product:*").ToArray();
                
                // AUTO-INDEXING: If Redis is empty, load from SQL
                if (keys.Length == 0)
                {
                    Console.WriteLine("Redis is empty. Auto-indexing products from SQL...");
                    // Reduced to 1 to prevent timeout on very slow machines
                    var products = await _context.Product.Include(p => p.Category).Take(1).ToListAsync();
                    
                    if (products.Count == 0) return; 

                    int count = 0;
                    foreach (var p in products)
                    {
                        count++;
                        Console.WriteLine($"Indexing product {count}/{products.Count}: {p.Name}...");
                        await IndexProductAsync(p);
                    }
                    
                    // Re-fetch keys
                    keys = server.Keys(pattern: "product:*").ToArray();
                }

                var newCache = new List<(string, float[], string)>();

                foreach (var key in keys)
                {
                    var hashEntries = await _redis.HashGetAllAsync(key);
                    var embeddingJson = hashEntries.FirstOrDefault(e => e.Name == "embedding").Value;
                    
                    if (embeddingJson.HasValue)
                    {
                        try 
                        {
                            var vector = JsonSerializer.Deserialize<float[]>(embeddingJson.ToString());
                            var name = hashEntries.FirstOrDefault(e => e.Name == "name").Value;
                            var desc = hashEntries.FirstOrDefault(e => e.Name == "description").Value;
                            var price = hashEntries.FirstOrDefault(e => e.Name == "price").Value;
                            
                            if (vector != null)
                            {
                                newCache.Add((key, vector, $"{name} ({price} DH): {desc}"));
                            }
                        } 
                        catch {}
                    }
                }

                _vectorCache = newCache;
                _lastCacheUpdate = DateTime.UtcNow;
                Console.WriteLine($"Cache loaded. Found {_vectorCache.Count} products.");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task<List<string>> SearchSimilarProductsAsync(float[] queryVector)
        {
            if (queryVector.Length == 0) return new List<string>();

            await EnsureCacheLoadedAsync();
            
            var results = new List<(double Score, string Content)>();

            foreach (var item in _vectorCache)
            {
                var score = CosineSimilarity(queryVector, item.Embedding);
                if (score > 0.45) // Slightly lower threshold to be safe
                {
                    results.Add((score, item.Content));
                }
            }

            return results.OrderByDescending(x => x.Score).Take(3).Select(x => x.Content).ToList();
        }

        private double CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length != v2.Length) return 0;
            double dot = 0, mag1 = 0, mag2 = 0;
            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                mag1 += v1[i] * v1[i];
                mag2 += v2[i] * v2[i];
            }
            return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
        }

        private async Task<string> CallOllamaChatAsync(string systemPrompt, string userPrompt)
        {
            var url = $"{_ollamaBaseUrl}/api/chat";
            
            var requestData = new
            {
                model = _chatModel,
                messages = new[] 
                { 
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try 
            {
                var response = await _httpClient.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode) 
                {
                   var error = await response.Content.ReadAsStringAsync();
                   return $"Error calling Ollama API: {response.StatusCode} - {error}";
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseString);
                
                return result?.Message?.Content ?? "No response from AI.";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // Helper classes for Ollama responses
        private class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; }
        }

        private class OllamaChatResponse
        {
            [JsonPropertyName("message")]
            public OllamaMessage Message { get; set; }
        }

        private class OllamaMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }
            [JsonPropertyName("content")]
            public string Content { get; set; }
        }
    }
}
