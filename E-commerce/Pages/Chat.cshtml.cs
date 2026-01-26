using E_commerce.Services.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace E_commerce.Pages
{
    public class ChatModel : PageModel
    {
        private readonly ISearchOrchestrator _searchOrchestrator;

        public ChatModel(ISearchOrchestrator searchOrchestrator)
        {
            _searchOrchestrator = searchOrchestrator;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAskAsync([FromBody] ChatRequest request)
        {
            try
            {
                Console.WriteLine($"[API] Chat request received: {request?.Message}");
                if (string.IsNullOrWhiteSpace(request?.Message))
                {
                    return new JsonResult(new { answer = "I didn't hear anything!" });
                }

                var response = await _searchOrchestrator.GetAnswerAsync(request.Message);
                Console.WriteLine("[API] Chat response generated successfully.");
                return new JsonResult(new { answer = response });
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[API] ERROR in ChatModel: {ex}");
                return new JsonResult(new { answer = "Sorry, an internal error occurred." }) { StatusCode = 500 };
            }
        }

        public class ChatRequest
        {
            public string Message { get; set; }
        }
    }
}
