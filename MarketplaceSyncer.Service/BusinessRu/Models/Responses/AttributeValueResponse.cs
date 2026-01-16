using System.Text.Json.Serialization;
using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public class AttributeValueResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("attribute_id")]
    public int AttributeId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sort")]
    public int Sort { get; set; }

    [JsonPropertyName("updated")]
    [JsonConverter(typeof(BusinessRuDateTimeConverter))]
    public DateTimeOffset Updated { get; set; }
}
