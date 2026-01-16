using System.Text.Json.Serialization;
using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public class GoodAttributeResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("good_id")]
    public int GoodId { get; set; }

    [JsonPropertyName("attribute_id")]
    public int AttributeId { get; set; }

    [JsonPropertyName("value_id")]
    public int? ValueId { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("updated")]
    [JsonConverter(typeof(BusinessRuDateTimeConverter))]
    public DateTimeOffset Updated { get; set; }
}
