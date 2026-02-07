using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Валюта (currencies).
/// </summary>
public sealed record CurrencyResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("short_name")] string? ShortName,
    [property: JsonPropertyName("name_iso")] string? NameIso,
    [property: JsonPropertyName("code_iso")] long? CodeIso, // Docs say 'long'
    [property: JsonPropertyName("symbol_iso")] long? SymbolIso, // Docs say 'long' (likely char code or similar, or nullable)
    [property: JsonPropertyName("okv")] string? Okv,
    [property: JsonPropertyName("default")] bool Default,
    [property: JsonPropertyName("user")] bool User,
    [property: JsonPropertyName("user_value")] decimal? UserValue, // Docs say float, assuming decimal for currency
    [property: JsonPropertyName("name1")] string? Name1,
    [property: JsonPropertyName("name2")] string? Name2,
    [property: JsonPropertyName("name3")] string? Name3
);
