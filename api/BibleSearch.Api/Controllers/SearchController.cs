
using Microsoft.AspNetCore.Mvc;
using BibleSearch.Api.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BibleSearch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        // Максимальна кількість кандидатів, які повертає Vector Search.
        private const int MaxVectorSearchResults = 10000;

        // Максимальна кількість релевантних чанків,
        // які передаються на другий етап пошуку.
        private const int MaxChunksForVerseSearch = 500;

        private const double VerseWeight = 0.7;
        private const double ChunkWeight = 0.3;

        private readonly MongoDbService _mongoDbService;
        private readonly EmbeddingService _embeddingService;

        private readonly double _chunkSimilarityThreshold;
        private readonly double _verseSimilarityThreshold;

        public SearchController(
            MongoDbService mongoDbService,
            EmbeddingService embeddingService,
            IConfiguration configuration)
        {
            _mongoDbService = mongoDbService;
            _embeddingService = embeddingService;

            _chunkSimilarityThreshold =
                configuration.GetValue<double?>(
                    "SearchSettings:ChunkSimilarityThreshold") ?? 0.70;

            _verseSimilarityThreshold =
                configuration.GetValue<double?>(
                    "SearchSettings:VerseSimilarityThreshold") ?? 0.75;
        }

        // ==========================================
        // ФІЛЬТРИ: СПИСОК БІБЛІЙНИХ КНИГ
        // ==========================================

        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            var documents = await _mongoDbService.Documents
                .Find(_ => true)
                .ToListAsync();

            var books = documents
                .Where(d => !string.IsNullOrWhiteSpace(d.Id))
                .OrderBy(d => d.Order)
                .Select(d => new
                {
                    documentId = d.Id,
                    title = d.Title,
                    testament = d.Testament,
                    order = d.Order
                })
                .ToList();

            return Ok(new
            {
                testaments = new[]
                {
                    new
                    {
                        value = "Old Testament",
                        label = "Старий Завіт"
                    },
                    new
                    {
                        value = "New Testament",
                        label = "Новий Завіт"
                    }
                },
                books
            });
        }

        // ==========================================
        // ОТРИМАННЯ ПОВНОГО БІБЛІЙНОГО РОЗДІЛУ
        // ==========================================

        [HttpGet("chapter/{documentId}/{chapter:int}")]
        public async Task<IActionResult> GetChapter(
            string documentId,
            int chapter)
        {
            if (!ObjectId.TryParse(documentId, out var documentObjectId))
                return BadRequest("Invalid document ID.");

            if (chapter < 1)
                return BadRequest(
                    "Chapter number must be greater than zero.");

            var pipeline = new[]
            {
                new BsonDocument(
                    "$match",
                    new BsonDocument
                    {
                        { "DocumentId", new BsonObjectId(documentObjectId) },
                        { "Chapter", chapter }
                    }),

                new BsonDocument(
                    "$sort",
                    new BsonDocument("Number", 1)),

                new BsonDocument(
                    "$project",
                    new BsonDocument
                    {
                        { "_id", 1 },
                        { "Book", 1 },
                        { "Chapter", 1 },
                        { "Number", 1 },
                        { "Text", 1 },
                        { "Translation", 1 }
                    })
            };

            var verses = await _mongoDbService.Verses
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();

            if (verses.Count == 0)
                return NotFound("Chapter not found.");

            var firstVerse = verses[0];

            return Ok(new
            {
                documentId,
                book = GetString(firstVerse, "Book"),
                chapter = GetInt(firstVerse, "Chapter"),
                translation = GetString(firstVerse, "Translation"),

                verses = verses.Select(verse => new
                {
                    id = verse["_id"].AsObjectId.ToString(),
                    number = GetInt(verse, "Number"),
                    text = GetString(verse, "Text")
                }).ToList()
            });
        }

        // ==========================================
        // СЕМАНТИЧНИЙ ПОШУК
        // ДВА ЕТАПИ ВЕКТОРНОГО ПОШУКУ
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> Search(
            [FromBody] SearchRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(
                    "Search query cannot be empty.");
            }

            // ==========================================
            // 1. ПЕРЕВІРКА ФІЛЬТРІВ
            // ==========================================

            string? testament = null;

            if (!string.IsNullOrWhiteSpace(request.Testament))
            {
                testament = NormalizeTestament(request.Testament);

                if (testament == null)
                {
                    return BadRequest(
                        "Testament must be Old Testament or New Testament.");
                }
            }

            ObjectId? documentObjectId = null;

            if (!string.IsNullOrWhiteSpace(request.DocumentId))
            {
                if (!ObjectId.TryParse(
                    request.DocumentId, out var parsedId))
                {
                    return BadRequest("Invalid document ID.");
                }

                documentObjectId = parsedId;
            }

            // ==========================================
            // 2. ВИЗНАЧЕННЯ ДОЗВОЛЕНИХ КНИГ
            // ==========================================

            var hasDocumentFilter =
                testament != null ||
                !string.IsNullOrWhiteSpace(request.Book) ||
                documentObjectId.HasValue;

            List<ObjectId> allowedDocumentIds = new();

            if (hasDocumentFilter)
            {
                var documents = await _mongoDbService.Documents
                    .Find(_ => true)
                    .ToListAsync();

                if (testament != null)
                {
                    documents = documents
                        .Where(d => string.Equals(
                            d.Testament,
                            testament,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(request.Book))
                {
                    documents = documents
                        .Where(d => string.Equals(
                            d.Title,
                            request.Book.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (documentObjectId.HasValue)
                {
                    documents = documents
                        .Where(d =>
                            d.Id == documentObjectId.Value.ToString())
                        .ToList();
                }

                allowedDocumentIds = documents
                    .Where(d => ObjectId.TryParse(d.Id, out _))
                    .Select(d => ObjectId.Parse(d.Id!))
                    .ToList();

                if (allowedDocumentIds.Count == 0)
                    return EmptyResult(request.Query);
            }

            // ==========================================
            // 3. ГЕНЕРАЦІЯ EMBEDDING ЗАПИТУ
            // ==========================================

            var queryEmbedding =
                await _embeddingService.GenerateEmbeddingAsync(
                    request.Query);

            if (queryEmbedding.Count != 1536)
            {
                return StatusCode(
                    500,
                    "Embedding dimensions do not match the vector index.");
            }

            var queryVector = new BsonArray(
                queryEmbedding.Select(
                    value => (BsonValue)new BsonDouble(value)));

            // ==========================================
            // ЕТАП 1. ВЕКТОРНИЙ ПОШУК ЧАНКІВ
            // ==========================================

            var chunkVectorDefinition = new BsonDocument
            {
                { "index", "chunk_vector_index" },
                { "path", "Embedding" },
                { "queryVector", queryVector },
                { "numCandidates", MaxVectorSearchResults },
                { "limit", MaxVectorSearchResults }
            };

            if (hasDocumentFilter)
            {
                chunkVectorDefinition["filter"] = new BsonDocument(
                    "DocumentId",
                    new BsonDocument(
                        "$in",
                        new BsonArray(
                            allowedDocumentIds.Select(
                                id => (BsonValue)new BsonObjectId(id)))));
            }

            var chunkPipeline = new[]
            {
                new BsonDocument(
                    "$vectorSearch",
                    chunkVectorDefinition),

                new BsonDocument(
                    "$project",
                    new BsonDocument
                    {
                        { "_id", 1 },
                        { "DocumentId", 1 },
                        { "Book", 1 },
                        {
                            "Score",
                            new BsonDocument(
                                "$meta",
                                "vectorSearchScore")
                        }
                    })
            };

            var chunkDocuments = await _mongoDbService.Chunks
                .Aggregate<BsonDocument>(chunkPipeline)
                .ToListAsync();

            // ==========================================
            // 4. ВІДБІР ЧАНКІВ ЗА ПОРОГОМ
            // ==========================================

            // Спочатку залишаємо всі чанки,
            // які пройшли встановлений поріг.

            var relevantChunks = chunkDocuments
                .Select(chunk => new
                {
                    Id = chunk["_id"].AsObjectId,
                    Score = chunk["Score"].ToDouble()
                })
                .Where(chunk =>
                    chunk.Score >= _chunkSimilarityThreshold)
                .ToList();

            if (relevantChunks.Count == 0)
                return EmptyResult(request.Query);

            // ==========================================
            // 5. ОБМЕЖЕННЯ КІЛЬКОСТІ ЧАНКІВ
            // ==========================================

            // Сортуємо чанки за схожістю.
            // Залишаємо максимум 500 найкращих.

            var selectedChunks = relevantChunks
                .OrderByDescending(chunk => chunk.Score)
                .Take(MaxChunksForVerseSearch)
                .ToList();

            // Створюємо словник оцінок лише для відібраних чанків.

            var chunkScoreDictionary = selectedChunks
                .GroupBy(chunk => chunk.Id)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Score);

            var chunkIds = chunkScoreDictionary.Keys
                .Select(id => (BsonValue)new BsonObjectId(id))
                .ToList();

            // ==========================================
            // ЕТАП 2. ВЕКТОРНИЙ ПОШУК ОКРЕМИХ ВІРШІВ
            // ==========================================

            var verseVectorFilter = new BsonDocument(
                "ChunkIds",
                new BsonDocument(
                    "$in",
                    new BsonArray(chunkIds)));

            if (hasDocumentFilter)
            {
                verseVectorFilter = new BsonDocument(
                    "$and",
                    new BsonArray
                    {
                        verseVectorFilter,

                        new BsonDocument(
                            "DocumentId",
                            new BsonDocument(
                                "$in",
                                new BsonArray(
                                    allowedDocumentIds.Select(
                                        id => (BsonValue)new BsonObjectId(id)))))
                    });
            }

            var verseVectorDefinition = new BsonDocument
            {
                { "index", "verse_vector_index" },
                { "path", "Embedding" },
                { "queryVector", queryVector },
                { "numCandidates", MaxVectorSearchResults },
                { "limit", MaxVectorSearchResults },
                { "filter", verseVectorFilter }
            };

            var versePipeline = new[]
            {
                new BsonDocument(
                    "$vectorSearch",
                    verseVectorDefinition),

                new BsonDocument(
                    "$project",
                    new BsonDocument
                    {
                        { "_id", 1 },
                        { "DocumentId", 1 },
                        { "Book", 1 },
                        { "Chapter", 1 },
                        { "Number", 1 },
                        { "Text", 1 },
                        { "Translation", 1 },
                        { "ChunkIds", 1 },
                        {
                            "VerseScore",
                            new BsonDocument(
                                "$meta",
                                "vectorSearchScore")
                        }
                    })
            };

            var verseDocuments = await _mongoDbService.Verses
                .Aggregate<BsonDocument>(versePipeline)
                .ToListAsync();

            // ==========================================
            // 6. ФІЛЬТРАЦІЯ ТА ОБ'ЄДНАННЯ ОЦІНОК
            // ==========================================

            var results = verseDocuments
                .Select(verse =>
                {
                    double verseScore =
                        verse["VerseScore"].ToDouble();

                    double chunkScore = 0;

                    if (verse.TryGetValue(
                        "ChunkIds", out var verseChunkIds) &&
                        verseChunkIds.IsBsonArray)
                    {
                        var matchingChunkScores =
                            verseChunkIds.AsBsonArray
                                .Where(value => value.IsObjectId)
                                .Where(value =>
                                    chunkScoreDictionary.ContainsKey(
                                        value.AsObjectId))
                                .Select(value =>
                                    chunkScoreDictionary[value.AsObjectId])
                                .ToList();

                        if (matchingChunkScores.Count > 0)
                        {
                            chunkScore = matchingChunkScores.Max();
                        }
                    }

                    return new
                    {
                        document = verse,
                        verseScore,
                        chunkScore,

                        finalScore =
                            VerseWeight * verseScore +
                            ChunkWeight * chunkScore
                    };
                })
                .Where(item =>
                    item.verseScore >= _verseSimilarityThreshold &&
                    item.chunkScore > 0)
                .OrderByDescending(item => item.finalScore)
                .Select(item => new
                {
                    id = item.document["_id"]
                        .AsObjectId.ToString(),

                    documentId = item.document["DocumentId"]
                        .AsObjectId.ToString(),

                    book = GetString(item.document, "Book"),
                    chapter = GetInt(item.document, "Chapter"),
                    number = GetInt(item.document, "Number"),
                    text = GetString(item.document, "Text"),

                    translation = GetString(
                        item.document,
                        "Translation"),

                    score = item.finalScore,

                    verseScore = item.verseScore,
                    chunkScore = item.chunkScore
                })
                .ToList();

            // ==========================================
            // 7. ПОВЕРНЕННЯ РЕЗУЛЬТАТІВ
            // ==========================================

            return Ok(new
            {
                query = request.Query,

                // Кількість чанків, які пройшли поріг.
                chunksFound = relevantChunks.Count,

                // Кількість чанків, переданих на другий етап.
                chunksUsedForVerseSearch = selectedChunks.Count,

                // Кількість кандидатів, знайдених серед віршів.
                versesCandidatesFound = verseDocuments.Count,

                // Кількість фінальних результатів.
                versesFound = results.Count,

                results
            });
        }

        // ==========================================
        // ПОРОЖНІЙ РЕЗУЛЬТАТ
        // ==========================================

        private IActionResult EmptyResult(string query)
        {
            return Ok(new
            {
                query,

                chunksFound = 0,
                chunksUsedForVerseSearch = 0,
                versesCandidatesFound = 0,
                versesFound = 0,

                results = new List<object>()
            });
        }

        // ==========================================
        // НОРМАЛІЗАЦІЯ НАЗВИ ЗАПОВІТУ
        // ==========================================

        private static string? NormalizeTestament(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "old testament" => "Old Testament",
                "старий завіт" => "Old Testament",

                "new testament" => "New Testament",
                "новий завіт" => "New Testament",

                _ => null
            };
        }

        // ==========================================
        // ДОПОМІЖНІ МЕТОДИ
        // ==========================================

        private static string GetString(
            BsonDocument document,
            string field)
        {
            return document.TryGetValue(field, out var value) &&
                   !value.IsBsonNull
                ? value.AsString
                : string.Empty;
        }

        private static int GetInt(
            BsonDocument document,
            string field)
        {
            return document.TryGetValue(field, out var value) &&
                   !value.IsBsonNull
                ? value.ToInt32()
                : 0;
        }
    }

    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;

        public int ChunkTopK { get; set; } = 5;

        public string? DocumentId { get; set; }

        public string? Testament { get; set; }

        public string? Book { get; set; }
    }
}
