using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BibleSearch.Api.Models
{
    public class Document
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Testament { get; set; } = string.Empty;

        public int Order { get; set; }

        public string Translation { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}