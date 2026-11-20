
using Microsoft.AspNetCore.Mvc;
using BibleSearch.Api.Services;

namespace BibleSearch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BibleImportController : ControllerBase
    {
        private readonly BibleImportService _importService;
        private readonly BibleTextParserService _parserService;
        private readonly IWebHostEnvironment _environment;

        public BibleImportController(
            BibleImportService importService,
            BibleTextParserService parserService,
            IWebHostEnvironment environment)
        {
            _importService = importService;
            _parserService = parserService;
            _environment = environment;
        }

        private async Task<List<Models.BibleImportData>> ParseBibleAsync()
        {
            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "Data",
                "Bible_Ohienko.txt");

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Bible source file was not found.",
                    filePath);
            }

            return await _parserService.ParseFileAsync(filePath);
        }

        [HttpPost("verse")]
        public async Task<IActionResult> ImportVerse(
            [FromBody] Models.Verse verse)
        {
            if (string.IsNullOrWhiteSpace(verse.Text))
            {
                return BadRequest("Verse text cannot be empty.");
            }

            var imported = await _importService.ImportVerseAsync(verse);

            return Ok(new
            {
                message = imported
                    ? "Verse imported successfully."
                    : "Verse already exists. Skipped.",
                imported
            });
        }

        [HttpPost("book/{order:int}")]
        public async Task<IActionResult> ImportBook(int order)
        {
            if (order < 1 || order > 66)
            {
                return BadRequest("Book order must be between 1 and 66.");
            }

            var books = await ParseBibleAsync();

            var book = books.FirstOrDefault(b => b.Order == order);

            if (book == null)
            {
                return NotFound("Book not found.");
            }

            var result = await _importService.ImportFullBibleAsync(
                new List<Models.BibleImportData> { book });

            return Ok(new
            {
                message = "Book import completed.",
                book = book.Book,
                result.TotalBooks,
                result.ProcessedBooks,
                result.CreatedBooks,
                result.ExistingBooks,
                result.ImportedVerses,
                result.SkippedVerses
            });
        }

        [HttpPost("books/{startOrder:int}/{endOrder:int}")]
        public async Task<IActionResult> ImportBooks(
            int startOrder,
            int endOrder)
        {
            if (startOrder < 1 || endOrder > 66 ||
                startOrder > endOrder)
            {
                return BadRequest(
                    "Provide a valid book range between 1 and 66.");
            }

            var books = await ParseBibleAsync();

            var selectedBooks = books
                .Where(b => b.Order >= startOrder &&
                            b.Order <= endOrder)
                .OrderBy(b => b.Order)
                .ToList();

            var result = await _importService.ImportFullBibleAsync(
                selectedBooks);

            return Ok(new
            {
                message = "Books import completed.",
                startOrder,
                endOrder,
                bookNames = selectedBooks.Select(b => b.Book),
                result.TotalBooks,
                result.ProcessedBooks,
                result.CreatedBooks,
                result.ExistingBooks,
                result.ImportedVerses,
                result.SkippedVerses
            });
        }

        [HttpPost("full-bible")]
        public async Task<IActionResult> ImportFullBible()
        {
            var books = await ParseBibleAsync();

            if (books.Count != 66)
            {
                return BadRequest(
                    "Bible parsing failed or did not return 66 books.");
            }

            var result = await _importService.ImportFullBibleAsync(books);

            return Ok(new
            {
                message = "Bible import completed successfully.",
                result.TotalBooks,
                result.ProcessedBooks,
                result.CreatedBooks,
                result.ExistingBooks,
                result.ImportedVerses,
                result.SkippedVerses
            });
        }
    }
}
