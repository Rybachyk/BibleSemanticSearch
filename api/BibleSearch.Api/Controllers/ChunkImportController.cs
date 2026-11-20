using Microsoft.AspNetCore.Mvc;
using BibleSearch.Api.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BibleSearch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChunkImportController : ControllerBase
    {
        private readonly MongoDbService _mongoDbService;
        private readonly ChunkImportService _chunkImportService;

        public ChunkImportController(
            MongoDbService mongoDbService,
            ChunkImportService chunkImportService)
        {
            _mongoDbService = mongoDbService;
            _chunkImportService = chunkImportService;
        }

        // Створення chunks із віршів, які вже збережені в MongoDB
        [HttpPost("bible/{documentId}")]
        public async Task<IActionResult> ImportBibleBook(
            string documentId)
        {
            if (!ObjectId.TryParse(documentId, out _))
                return BadRequest("Invalid document ID.");

            var document = await _mongoDbService.Documents
                .Find(d => d.Id == documentId)
                .FirstOrDefaultAsync();

            if (document == null)
                return NotFound("Document not found.");

            var importedChunks =
                await _chunkImportService.ImportBibleBookFromFileAsync(
                    documentId);

            return Ok(new
            {
                message = "Bible book chunks generated successfully.",
                documentId,
                documentTitle = document.Title,
                importedChunks
            });
        }
    }
}