using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Единица измерения (measures).
/// </summary>
public sealed record MeasureResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("short_name")] string? ShortName,
    [property: JsonPropertyName("default")] bool Default,
    [property: JsonPropertyName("okei")] long? Okei,
    [property: JsonPropertyName("archive")] bool Archive,
    [property: JsonPropertyName("updated")] DateTimeOffset? Updated,
    [property: JsonPropertyName("deleted")] bool Deleted
);
