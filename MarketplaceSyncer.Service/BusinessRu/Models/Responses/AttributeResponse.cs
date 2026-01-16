using System.Text.Json.Serialization;
using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public class AttributeResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("selectable")]
    public bool Selectable { get; set; }

    [JsonPropertyName("archive")]
    public bool Archive { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sort")]
    public int Sort { get; set; }

    [JsonPropertyName("updated")]
    [JsonConverter(typeof(BusinessRuDateTimeConverter))]
    public DateTimeOffset Updated { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}
