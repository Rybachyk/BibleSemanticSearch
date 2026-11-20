
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BibleSearch.Api.Models
{
    public class Verse
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string DocumentId { get; set; } = string.Empty;

        public string Book { get; set; } = string.Empty;

        public int Chapter { get; set; }

        public int Number { get; set; }

        public string Text { get; set; } = string.Empty;

        public string Translation { get; set; } = string.Empty;

        // ID chunks, до яких належить цей вірш.
        public List<ObjectId> ChunkIds { get; set; } = new();

        public List<float> Embedding { get; set; } = new();
    }
}
