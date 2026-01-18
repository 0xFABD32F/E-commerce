using E_commerce.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace E_commerce.Pages
{
    public class ChatModel : PageModel
    {
        private readonly IAIService _aiService;

        public ChatModel(IAIService aiService)
        {
            _aiService = aiService;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAskAsync([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return new JsonResult(new { answer = "I didn't hear anything!" });
            }

            var response = await _aiService.ChatAsync(request.Message);
            return new JsonResult(new { answer = response });
        }

        // Helper to index products manually (admin/dev utility)
        // Call via POST /Chat?handler=IndexAll
        // In a real app, this would be behind auth or a background job
        public async Task<IActionResult> OnPostIndexAllAsync([FromServices] E_commerce.Data.E_commerceContext context)
        {
            var products = context.Product.ToList(); // Load all for simplicity
            foreach (var p in products)
            {
                await _aiService.IndexProductAsync(p);
            }
            return new JsonResult(new { status = $"Indexed {products.Count} products." });
        }

        public class ChatRequest
        {
            public string Message { get; set; }
        }
    }
}
