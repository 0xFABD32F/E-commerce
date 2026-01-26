using Microsoft.AspNetCore.Http;
//using E_commerce.Data.Services;
using System.Text.RegularExpressions;

namespace E_commerce.Services.AI
{
    public class ProductSearchOrchestrator : ISearchOrchestrator
    {
        private readonly IChatService _chatService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;
        private readonly IChatContextService _chatContextService;
        
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProductSearchOrchestrator(
            IChatService chatService, 
            IEmbeddingService embeddingService, 
            IVectorStore vectorStore,
            IChatContextService chatContextService,
            IHttpContextAccessor httpContextAccessor)
        {
            _chatService = chatService;
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
            _chatContextService = chatContextService;            
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> GetAnswerAsync(string userQuery)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine($"[PERF] Start GetAnswerAsync for query: {userQuery}");           

            // 0. Handle Greetings matches : basic regex to avoid API calls
            if (IsGreeting(userQuery))
            {
                Console.WriteLine($"[PERF] IsGreeting hit. Total: {swTotal.ElapsedMilliseconds}ms");
                return "Hello! I am your AI Shopping Assistant. Ask me about our musical instruments !";
            }            

            // 1. Multi-Query Generation
            var swQuery = System.Diagnostics.Stopwatch.StartNew();
            var queries = await GenerateQueriesAsync(userQuery);
            swQuery.Stop();
            Console.WriteLine($"[PERF] GenerateQueriesAsync took {swQuery.ElapsedMilliseconds}ms. Count: {queries.Count}");
            
            queries.Add(userQuery); // Always include the original query (useful if the LLM failed to generate relevant questions)

            // 2. Vector Search for all queries
            var searchResults = new List<SearchResult>();
            
            // 1.5 Extract Category context (if any)
            string? categoryFilter = ExtractCategory(userQuery);
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                Console.WriteLine($"[PERF] Extracted Category Filter: {categoryFilter}");
            }

            var swSearchLoop = System.Diagnostics.Stopwatch.StartNew();
            
            // 2.1 Bulk Generate Embeddings
            var swEmb = System.Diagnostics.Stopwatch.StartNew();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(queries);
            swEmb.Stop();
            Console.WriteLine($"[PERF] GenerateEmbeddingsAsync (Bulk) for {queries.Count} queries took {swEmb.ElapsedMilliseconds}ms");

            // 2.2 Parallel Search
            var searchTasks = embeddings.Select(async (embedding, index) => 
            {
                if (embedding.Length > 0)
                {
                    var swVec = System.Diagnostics.Stopwatch.StartNew();
                    // Pass the category filter to the search ===> better similarity results
                    var results = await _vectorStore.SearchAsync(embedding, limit: 3, categoryFilter);
                    swVec.Stop();
                    Console.WriteLine($"[PERF] VectorStore.SearchAsync for query '{queries[index]}' took {swVec.ElapsedMilliseconds}ms");
                    return results;
                }
                else
                {
                    Console.WriteLine($"[PERF] Embedding empty for '{queries[index]}'. Skipping search.");
                    return new List<SearchResult>();
                }
            });

            var taskResults = await Task.WhenAll(searchTasks);
            swSearchLoop.Stop();
            Console.WriteLine($"[PERF] Total Search Phase took {swSearchLoop.ElapsedMilliseconds}ms");
            
            foreach (var res in taskResults)
            {
                searchResults.AddRange(res);
            }

            // 3. Deduplication and Context Building
            var uniqueResults = searchResults
                .GroupBy(r => r.Id)
                .Select(g => g.First()) // Take best scoring instance of each product
                .OrderByDescending(r => r.Score)
                .Take(5) // Max 5 products in context
                .ToList();

            if (uniqueResults.Count == 0)
            {
                Console.WriteLine($"[PERF] No results found. Total: {swTotal.ElapsedMilliseconds}ms");
                return "I'm sorry, I couldn't find any products matching your description.";
            }        

            var context = string.Join("\n\n", uniqueResults.Select(r => $"Product: {r.Content}"));

            // 4. Final Answer Generation
            var systemPrompt = "You are a helpful shopping assistant for a music store. " +
                               "Use the provided Product Context to answer the user's question. " +
                               "If the answer is not in the context, say you don't know.";
            
            var userPrompt = $"Context:\n{context}\n\nUser Question: {userQuery}";

            Console.WriteLine($"[DEBUG] Final Context Length: {context.Length}");
            if (context.Length > 0)
                Console.WriteLine($"[DEBUG] Context Preview: {context.Substring(0, Math.Min(500, context.Length))}...");


            var swChat = System.Diagnostics.Stopwatch.StartNew();
            var result = await _chatService.GetChatCompletionAsync(systemPrompt, userPrompt);
            swChat.Stop();
            Console.WriteLine($"[PERF] Final Chat Completion took {swChat.ElapsedMilliseconds}ms");
            
            swTotal.Stop();
            Console.WriteLine($"[PERF] GetAnswerAsync Total Time: {swTotal.ElapsedMilliseconds}ms");

            return result;
        }

        private async Task<List<string>> GenerateQueriesAsync(string originalQuery)
        {
            var prompt = $"You are a helpful assistant. Generate 3 varied search queries based on the user question to retrieve relevant products from a music store database. " +
                         $"User Question: \"{originalQuery}\". " +
                         $"Output ONLY the 3 queries, one per line. Do not number them. Do not add any other text.";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _chatService.GetChatCompletionAsync("You are a query generator.", prompt);
            sw.Stop();
            Console.WriteLine($"[PERF] GenerateQueriesAsync (LLM Only) took {sw.ElapsedMilliseconds}ms");
            
            var queries = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .Take(3)
                                  .ToList();
            
           // Fallback if LLM fails or returns garbage
           if (queries.Count == 0) return new List<string>();

           return queries;
        }

        private string? ExtractCategory(string query)
        {
            // Known categories: Amplifiers, Drum Kit, Guitars, Pedals, Studio Software
            // Using regex to be robust against "guitar", "guitars", "amp", "amplifiers"
            
            if (Regex.IsMatch(query, @"\b(amplifiers?|amps?)\b", RegexOptions.IgnoreCase)) return "Amplifiers";
            if (Regex.IsMatch(query, @"\b(drum\s?kit|drums?)\b", RegexOptions.IgnoreCase)) return "Drum Kit";
            if (Regex.IsMatch(query, @"\b(guitars?)\b", RegexOptions.IgnoreCase)) return "Guitars";
            if (Regex.IsMatch(query, @"\b(pedals?|effects?)\b", RegexOptions.IgnoreCase)) return "Pedals";
            if (Regex.IsMatch(query, @"\b(studio\s?software|software)\b", RegexOptions.IgnoreCase)) return "Studio Software";

            return null;
        }

        private bool IsGreeting(string text)
        {
            var greetings = new[] { "hello", "hi", "hey", "good morning" };
            return greetings.Any(g => text.Trim().ToLower().StartsWith(g));
        }

    }
}
