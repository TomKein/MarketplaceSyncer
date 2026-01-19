using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Атрибут товара из Business.ru (attributesforgoods).
/// </summary>
public record AttributeResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("selectable")] bool Selectable,
    [property: JsonPropertyName("archive")] bool Archive,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("sort")] long Sort,
    [property: JsonPropertyName("updated")] DateTimeOffset? Updated,
    [property: JsonPropertyName("deleted")] bool Deleted
);
