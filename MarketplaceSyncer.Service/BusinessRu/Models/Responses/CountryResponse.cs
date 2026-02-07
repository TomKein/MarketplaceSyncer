using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Страна (countries).
/// </summary>
public sealed record CountryResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("international_name")] string? InternationalName,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("alfa2")] string? Alfa2,
    [property: JsonPropertyName("alfa3")] string? Alfa3
    // Note: 'updated' field is not mentioned in docs for Country, so omitting.
);
