
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BibleSearch.Api.Models
{
    public class Chunk
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string DocumentId { get; set; } = string.Empty;

        public int Index { get; set; }

        public string Book { get; set; } = string.Empty;

        public int ChapterStart { get; set; }

        public int VerseStart { get; set; }

        public int ChapterEnd { get; set; }

        public int VerseEnd { get; set; }

        public List<ObjectId> VerseIds { get; set; } = new();

        public string Text { get; set; } = string.Empty;

        public List<float> Embedding { get; set; } = new();
    }
}
