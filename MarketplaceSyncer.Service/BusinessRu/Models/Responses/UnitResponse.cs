using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public record UnitResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("code")] string Code
);
