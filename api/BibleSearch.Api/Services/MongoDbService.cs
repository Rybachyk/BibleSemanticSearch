using MongoDB.Driver;
using BibleSearch.Api.Models;

namespace BibleSearch.Api.Services
{
    public class MongoDbService
    {
        public IMongoCollection<Document> Documents { get; }

        public IMongoCollection<Chunk> Chunks { get; }

        public IMongoCollection<Verse> Verses { get; }

        public MongoDbService(IMongoDatabase database)
        {
            Documents = database.GetCollection<Document>("documents");

            Chunks = database.GetCollection<Chunk>("chunks");

            Verses = database.GetCollection<Verse>("verses");
        }
    }
}