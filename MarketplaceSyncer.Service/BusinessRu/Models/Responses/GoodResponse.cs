using System.Text.Json.Serialization;

using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record GoodResponse(
    [property: JsonPropertyName("id")] 
    long Id,
    
    [property: JsonPropertyName("name")] 
    string? Name = null,
    
    [property: JsonPropertyName("part_number")] 
    string? PartNumber = null,
    
    [property: JsonPropertyName("store_code")] 
    string? StoreCode = null,
    
    [property: JsonPropertyName("archive")] 
    bool Archive = false,
    
    [property: JsonPropertyName("prices")] 
    GoodPrice[]? Prices = null,

    [property: JsonPropertyName("remains")]
    object? Remains = null,
    
    [property: JsonPropertyName("attributes")]
    GoodAttributeResponse[]? Attributes = null,
    
    [property: JsonPropertyName("updated_remains_prices")]
    [property: JsonConverter(typeof(BusinessRuDateTimeConverter))]
    DateTimeOffset? UpdatedRemainsPrices = null,

    [property: JsonPropertyName("images")]
    GoodImageResponse[]? Images = null);

public sealed record GoodPrice(
    [property: JsonPropertyName("price")]
    decimal? Price,
    
    // User JSON shows nested price_type object
    [property: JsonPropertyName("price_type")]
    GoodPriceType? PriceType,
    
    // Fallback or if API behaves differently
    [property: JsonPropertyName("type_id")]
    string? TypeId,
    
    [property: JsonPropertyName("currency")]
    string? Currency);

public sealed record GoodPriceType(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("name")]
    string? Name,
    [property: JsonPropertyName("currency")]
    GoodPriceCurrency? Currency);

public sealed record GoodPriceCurrency(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("name")]
    string? Name,
    [property: JsonPropertyName("short_name")]
    string? ShortName);
