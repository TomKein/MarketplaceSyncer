using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Значение атрибута товара из Business.ru (attributesforgoodsvalues).
/// </summary>
public record AttributeValueResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("attribute_id")] long AttributeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sort")] long Sort,
    [property: JsonPropertyName("updated")] DateTimeOffset? Updated
);
