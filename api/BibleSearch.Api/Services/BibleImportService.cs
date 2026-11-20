
using BibleSearch.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BibleSearch.Api.Services
{
    public class BibleImportService
    {
        private readonly MongoDbService _mongoDbService;
        private readonly EmbeddingService _embeddingService;

        public BibleImportService(
            MongoDbService mongoDbService,
            EmbeddingService embeddingService)
        {
            _mongoDbService = mongoDbService;
            _embeddingService = embeddingService;
        }

        public async Task<bool> ImportVerseAsync(Verse verse)
        {
            // Перевіряємо, чи існує такий вірш.
            var filter = Builders<Verse>.Filter.And(
                Builders<Verse>.Filter.Eq(
                    v => v.DocumentId, verse.DocumentId),
                Builders<Verse>.Filter.Eq(
                    v => v.Chapter, verse.Chapter),
                Builders<Verse>.Filter.Eq(
                    v => v.Number, verse.Number),
                Builders<Verse>.Filter.Eq(
                    v => v.Translation, verse.Translation)
            );

            var existingVerse = await _mongoDbService.Verses
                .Find(filter)
                .FirstOrDefaultAsync();

            if (existingVerse != null)
            {
                return false;
            }

            // Генеруємо embedding для тексту вірша.
            var embedding =
                await _embeddingService.GenerateEmbeddingAsync(
                    verse.Text);

            if (embedding.Count != 1536)
            {
                throw new Exception(
                    $"Invalid embedding dimensions: {embedding.Count}. Expected 1536.");
            }

            verse.Embedding = embedding;

            await _mongoDbService.Verses.InsertOneAsync(verse);

            return true;
        }

        public async Task<FullBibleImportResult> ImportFullBibleAsync(
            List<BibleImportData> books)
        {
            var result = new FullBibleImportResult();

            foreach (var book in books.OrderBy(b => b.Order))
            {
                // Шукаємо документ цієї книги.
                var documentFilter =
                    Builders<Document>.Filter.And(
                        Builders<Document>.Filter.Eq(
                            d => d.Title, book.Book),
                        Builders<Document>.Filter.Eq(
                            d => d.Translation, book.Translation)
                    );

                var document = await _mongoDbService.Documents
                    .Find(documentFilter)
                    .FirstOrDefaultAsync();

                // Якщо документа немає, створюємо його.
                if (document == null)
                {
                    document = new Document
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = book.Book,
                        Source = "Bible_Ohienko.txt",
                        Testament = book.Testament,
                        Order = book.Order,
                        Translation = book.Translation
                    };

                    await _mongoDbService.Documents.InsertOneAsync(
                        document);

                    result.CreatedBooks++;
                }
                else
                {
                    result.ExistingBooks++;
                }

                // Імпортуємо всі розділи та вірші цієї книги.
                foreach (var chapter in book.Chapters)
                {
                    foreach (var verseData in chapter.Verses)
                    {
                        var verse = new Verse
                        {
                            DocumentId = document.Id!,
                            Book = book.Book,
                            Chapter = chapter.Chapter,
                            Number = verseData.Number,
                            Text = verseData.Text,
                            Translation = book.Translation
                        };

                        var imported = await ImportVerseAsync(verse);

                        if (imported)
                        {
                            result.ImportedVerses++;
                        }
                        else
                        {
                            result.SkippedVerses++;
                        }
                    }
                }

                result.ProcessedBooks++;
            }

            result.TotalBooks = books.Count;

            return result;
        }
    }

    public class FullBibleImportResult
    {
        public int TotalBooks { get; set; }
        public int ProcessedBooks { get; set; }
        public int CreatedBooks { get; set; }
        public int ExistingBooks { get; set; }
        public int ImportedVerses { get; set; }
        public int SkippedVerses { get; set; }
    }
}
