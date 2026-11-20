
using Microsoft.AspNetCore.Mvc;
using BibleSearch.Api.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BibleSearch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly MongoDbService _mongoDbService;

        public DocumentsController(MongoDbService mongoDbService)
        {
            _mongoDbService = mongoDbService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateDocument(
            [FromBody] Models.Document document)
        {
            if (string.IsNullOrWhiteSpace(document.Title))
            {
                return BadRequest("Book title cannot be empty.");
            }

            await _mongoDbService.Documents.InsertOneAsync(document);

            return Ok(new
            {
                message = "Book created successfully.",
                id = document.Id,
                title = document.Title
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents = await _mongoDbService.Documents
                .Find(_ => true)
                .ToListAsync();

            return Ok(documents);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocumentById(string id)
        {
            if (!ObjectId.TryParse(id, out _))
            {
                return BadRequest("Invalid document ID.");
            }

            var document = await _mongoDbService.Documents
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();

            if (document == null)
            {
                return NotFound("Document not found.");
            }

            return Ok(document);
        }

        // Get verses from a specific book
        [HttpGet("{id}/verses")]
        public async Task<IActionResult> GetVerses(
            string id,
            [FromQuery] int? chapter)
        {
            if (!ObjectId.TryParse(id, out _))
            {
                return BadRequest("Invalid document ID.");
            }

            var document = await _mongoDbService.Documents
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();

            if (document == null)
            {
                return NotFound("Document not found.");
            }

            var filter = Builders<Models.Verse>.Filter
                .Eq(v => v.DocumentId, id);

            if (chapter.HasValue)
            {
                if (chapter.Value < 1)
                {
                    return BadRequest("Chapter must be greater than zero.");
                }

                filter &= Builders<Models.Verse>.Filter
                    .Eq(v => v.Chapter, chapter.Value);
            }

            var verses = await _mongoDbService.Verses
                .Find(filter)
                .SortBy(v => v.Chapter)
                .ThenBy(v => v.Number)
                .ToListAsync();

            return Ok(verses);
        }
    }
}
