namespace AntiFraud.Core.VectorizedReference.Models;

using System.Text.Json.Serialization;

public sealed record VectorizedReferenceFileModel(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("vector")] float[] Vector
);