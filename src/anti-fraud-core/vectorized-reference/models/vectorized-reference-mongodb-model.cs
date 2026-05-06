using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AntiFraud.Core.VectorizedReference.Models;

public record VectorizedReferenceMongoDBModel(
    [property: BsonElement("v")] float[] Vector,
    [property: BsonElement("f")] bool IsFraud
)
{
    [BsonId]
    [BsonIgnoreIfDefault]
    public ObjectId Id { get; init; }
}