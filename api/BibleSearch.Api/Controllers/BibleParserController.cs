
using BibleSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BibleSearch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BibleParserController : ControllerBase
    {
        private readonly BibleTextParserService _parser;
        private readonly IWebHostEnvironment _environment;

        public BibleParserController(
            BibleTextParserService parser,
            IWebHostEnvironment environment)
        {
            _parser = parser;
            _environment = environment;
        }

        [HttpGet("preview")]
        public async Task<IActionResult> Preview()
        {
            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "Data",
                "Bible_Ohienko.txt");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new
                {
                    message = "Bible text file was not found.",
                    path = filePath
                });
            }

            var books = await _parser.ParseFileAsync(filePath);

            var totalChapters = books.Sum(
                book => book.Chapters.Count);

            var totalVerses = books.Sum(
                book => book.Chapters.Sum(
                    chapter => chapter.Verses.Count));

            var chapterIssues = books
                .SelectMany(book =>
                {
                    var numbers = book.Chapters
                        .Select(chapter => chapter.Chapter)
                        .OrderBy(number => number)
                        .ToList();

                    var max = numbers.Count > 0
                        ? numbers.Max()
                        : 0;

                    var missing = Enumerable.Range(1, max)
                        .Except(numbers)
                        .ToList();

                    var duplicates = numbers
                        .GroupBy(number => number)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key)
                        .ToList();

                    if (!missing.Any() && !duplicates.Any())
                        return Enumerable.Empty<object>();

                    return new[]
                    {
                        (object)new
                        {
                            book = book.Book,
                            order = book.Order,
                            missingChapters = missing,
                            duplicateChapters = duplicates
                        }
                    };
                })
                .ToList();

            var verseIssues = books
                .SelectMany(book => book.Chapters
                    .Select(chapter =>
                    {
                        var numbers = chapter.Verses
                            .Select(verse => verse.Number)
                            .OrderBy(number => number)
                            .ToList();

                        var max = numbers.Count > 0
                            ? numbers.Max()
                            : 0;

                        var missing = Enumerable.Range(1, max)
                            .Except(numbers)
                            .ToList();

                        var duplicates = numbers
                            .GroupBy(number => number)
                            .Where(group => group.Count() > 1)
                            .Select(group => group.Key)
                            .ToList();

                        if (!missing.Any() && !duplicates.Any())
                            return null;

                        return new
                        {
                            book = book.Book,
                            chapter = chapter.Chapter,
                            missingVerses = missing,
                            duplicateVerses = duplicates
                        };
                    })
                    .Where(issue => issue != null))
                .ToList();

            var firstBook = books.FirstOrDefault();
            var lastBook = books.LastOrDefault();

            return Ok(new
            {
                translation = "Ohienko",

                totalBooks = books.Count,
                totalChapters,
                totalVerses,

                validation = new
                {
                    chapterIssuesCount = chapterIssues.Count,
                    verseIssuesCount = verseIssues.Count,
                    chapterIssues,
                    verseIssues
                },

                books = books.Select(book => new
                {
                    book.Book,
                    book.Order,
                    chapters = book.Chapters.Count,
                    verses = book.Chapters.Sum(
                        chapter => chapter.Verses.Count)
                }).ToList(),

                firstBook = firstBook == null ? null : new
                {
                    firstBook.Book,
                    firstBook.Order,
                    chapters = firstBook.Chapters.Count,
                    verses = firstBook.Chapters.Sum(
                        chapter => chapter.Verses.Count),

                    sampleVerses = firstBook.Chapters
                        .FirstOrDefault()?.Verses
                        .Take(3)
                },

                lastBook = lastBook == null ? null : new
                {
                    lastBook.Book,
                    lastBook.Order,
                    chapters = lastBook.Chapters.Count,
                    verses = lastBook.Chapters.Sum(
                        chapter => chapter.Verses.Count)
                }
            });
        }
    }
}
