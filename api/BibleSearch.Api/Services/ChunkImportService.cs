
using BibleSearch.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BibleSearch.Api.Services
{
    public class ChunkImportService
    {
        private const int ChunkSize = 800;
        private const int Overlap = 100;
        private const int EmbeddingDimensions = 1536;

        private readonly MongoDbService _mongoDbService;
        private readonly EmbeddingService _embeddingService;

        public ChunkImportService(
            MongoDbService mongoDbService,
            EmbeddingService embeddingService)
        {
            _mongoDbService = mongoDbService;
            _embeddingService = embeddingService;
        }

        // Створення chunks на основі віршів із MongoDB.
        public async Task<int> ImportBibleBookFromFileAsync(
            string documentId)
        {
            if (!ObjectId.TryParse(documentId, out _))
            {
                throw new ArgumentException("Invalid document ID.");
            }

            var verses = await _mongoDbService.Verses
                .Find(v => v.DocumentId == documentId)
                .SortBy(v => v.Chapter)
                .ThenBy(v => v.Number)
                .ToListAsync();

            if (verses.Count == 0)
            {
                return 0;
            }

            // Перевіряємо, чи chunks і зв'язки вже існують.
            var existingChunks = await _mongoDbService.Chunks
                .Find(c => c.DocumentId == documentId)
                .ToListAsync();

            var existingChunkIds = existingChunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .Select(c => ObjectId.Parse(c.Id!))
                .ToHashSet();

            var verseIds = verses
                .Where(v => !string.IsNullOrWhiteSpace(v.Id))
                .Select(v => ObjectId.Parse(v.Id!))
                .ToHashSet();

            bool relationsValid =
                existingChunks.Count > 0 &&
                existingChunks.All(c => c.VerseIds.Count > 0) &&
                verses.All(v => v.ChunkIds.Count > 0) &&
                verses.All(v => v.ChunkIds.All(
                    id => existingChunkIds.Contains(id))) &&
                existingChunks.All(c => c.VerseIds.All(
                    id => verseIds.Contains(id)));

            if (relationsValid)
            {
                return existingChunks.Count;
            }

            if (verseIds.Count != verses.Count)
            {
                throw new Exception(
                    "Some verses do not have valid MongoDB IDs.");
            }

            var chunks = new List<Chunk>();

            int start = 0;
            int chunkIndex = 0;

            while (start < verses.Count)
            {
                int end = start;
                int currentLength = 0;

                // Формуємо chunk приблизно на 800 символів.
                while (end < verses.Count &&
                       (end == start || currentLength < ChunkSize))
                {
                    currentLength +=
                        FormatVerse(verses[end]).Length +
                        Environment.NewLine.Length;

                    end++;
                }

                var chunkVerses = verses
                    .Skip(start)
                    .Take(end - start)
                    .ToList();

                var chunkText = string.Join(
                    Environment.NewLine,
                    chunkVerses.Select(FormatVerse));

                var embedding =
                    await _embeddingService.GenerateEmbeddingAsync(
                        chunkText);

                if (embedding.Count != EmbeddingDimensions)
                {
                    throw new Exception(
                        $"Invalid embedding dimensions: {embedding.Count}.");
                }

                var chunk = new Chunk
                {
                    Id = ObjectId.GenerateNewId().ToString(),

                    DocumentId = documentId,
                    Index = chunkIndex,
                    Book = chunkVerses[0].Book,

                    ChapterStart = chunkVerses[0].Chapter,
                    VerseStart = chunkVerses[0].Number,

                    ChapterEnd = chunkVerses[^1].Chapter,
                    VerseEnd = chunkVerses[^1].Number,

                    VerseIds = chunkVerses
                        .Select(v => ObjectId.Parse(v.Id!))
                        .ToList(),

                    Text = chunkText,
                    Embedding = embedding
                };

                chunks.Add(chunk);

                chunkIndex++;

                if (end >= verses.Count)
                {
                    break;
                }

                // Перекриття між сусідніми chunks.
                int nextStart = end;
                int overlapLength = 0;

                while (nextStart > start + 1 &&
                       overlapLength < Overlap)
                {
                    nextStart--;

                    overlapLength +=
                        FormatVerse(verses[nextStart]).Length +
                        Environment.NewLine.Length;
                }

                start = nextStart;
            }

            // Формуємо зворотний зв'язок Verse -> Chunk.
            var verseChunkMap = verses.ToDictionary(
                v => ObjectId.Parse(v.Id!),
                _ => new List<ObjectId>());

            foreach (var chunk in chunks)
            {
                var chunkId = ObjectId.Parse(chunk.Id!);

                foreach (var verseId in chunk.VerseIds)
                {
                    verseChunkMap[verseId].Add(chunkId);
                }
            }

            // Спочатку вставляємо нові chunks.
            await _mongoDbService.Chunks.InsertManyAsync(chunks);

            // Оновлюємо ChunkIds у кожному вірші.
            var updates = new List<WriteModel<Verse>>();

            foreach (var entry in verseChunkMap)
            {
                var filter = Builders<Verse>.Filter.Eq(
                    v => v.Id,
                    entry.Key.ToString());

                var update = Builders<Verse>.Update.Set(
                    v => v.ChunkIds,
                    entry.Value);

                updates.Add(
                    new UpdateOneModel<Verse>(filter, update));
            }

            if (updates.Count > 0)
            {
                await _mongoDbService.Verses.BulkWriteAsync(updates);
            }

            // Після успішного оновлення зв'язків видаляємо старі chunks.
            await _mongoDbService.Chunks.DeleteManyAsync(
                c => c.DocumentId == documentId &&
                     !chunks.Select(x => x.Id).Contains(c.Id));

            return chunks.Count;
        }

        private static string FormatVerse(Verse verse)
        {
            return $"{verse.Book} {verse.Chapter}:{verse.Number} {verse.Text}";
        }
    }
}
