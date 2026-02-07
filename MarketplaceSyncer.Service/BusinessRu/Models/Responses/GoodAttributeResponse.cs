using System.Text.Json.Serialization;
using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Привязка атрибута к товару (goodsattributes).
/// </summary>
public record GoodAttributeResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("good_id")] long GoodId,
    [property: JsonPropertyName("attribute_id")] long AttributeId,
    [property: JsonPropertyName("value_id")] long? ValueId,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("updated")] 
    [property: JsonConverter(typeof(BusinessRuDateTimeConverter))] 
    DateTimeOffset? Updated
);
