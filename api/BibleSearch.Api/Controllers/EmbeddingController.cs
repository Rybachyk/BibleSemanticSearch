using Microsoft.AspNetCore.Mvc;
using BibleSearch.Api.Services;

namespace BibleSearch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmbeddingController : ControllerBase
    {
        private readonly EmbeddingService _embeddingService;

        public EmbeddingController(EmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        [HttpPost]
        public async Task<IActionResult> Generate([FromBody] EmbeddingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("Text cannot be empty.");
            }

            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                request.Text);

            return Ok(new
            {
                dimensions = embedding.Count,
                preview = embedding.Take(5)
            });
        }
    }

    public class EmbeddingRequest
    {
        public string Text { get; set; } = string.Empty;
    }
}