using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace E_commerce.Services.AI
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaEmbeddingService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _model = config["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var url = $"{_baseUrl}/api/embeddings";
            var requestData = new { model = _model, prompt = text };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsync(url, content);
                sw.Stop();
                Console.WriteLine($"[PERF] OllamaEmbeddingService response received in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode) 
                {
                    Console.WriteLine($"[PERF] OllamaEmbeddingService failed. Code: {response.StatusCode}");
                    return Array.Empty<float>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaResponse>(json);
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PERF] OllamaEmbeddingService Exception: {ex.Message}");
                return Array.Empty<float>();
            }
        }



        public async Task<List<float[]>> GenerateEmbeddingsAsync(IList<string> texts)
        {
            var tasks = texts.Select(t => GenerateEmbeddingAsync(t));
            var results = await Task.WhenAll(tasks);
            return new List<float[]>(results);
        }

        private class OllamaResponse
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; }
        }
    }
}
