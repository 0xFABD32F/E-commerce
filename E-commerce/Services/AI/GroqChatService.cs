using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace E_commerce.Services.AI
{
    public class GroqChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public GroqChatService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _baseUrl = config["ExternalLLM:BaseUrl"] ?? "https://api.groq.com/openai/v1";
            _apiKey = config["ExternalLLM:ApiKey"] ?? "";
            _model = config["ExternalLLM:ChatModel"] ?? "llama3-70b-8192";
        }

        public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt)
        {
            var url = $"{_baseUrl}/chat/completions";
            var requestData = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.7
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };

            // Ensure Auth header is added
            if (!string.IsNullOrEmpty(_apiKey))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Console.WriteLine($"[PERF] GroqChatService sending request to {_baseUrl}...");
                var response = await _httpClient.SendAsync(requestMessage);
                sw.Stop();
                Console.WriteLine($"[PERF] GroqChatService response received in {sw.ElapsedMilliseconds}ms. Status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error: {response.StatusCode} - {error}";
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GroqResponse>(json);
                return result?.Choices?[0]?.Message?.Content ?? "No response.";
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }

        private class GroqResponse
        {
            [JsonPropertyName("choices")]
            public GroqChoice[] Choices { get; set; }
        }

        private class GroqChoice
        {
            [JsonPropertyName("message")]
            public GroqMessage Message { get; set; }
        }

        private class GroqMessage
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }
        }
    }
}
