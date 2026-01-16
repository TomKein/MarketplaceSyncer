using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record Good(
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
    object? Attributes = null,
    
    [property: JsonPropertyName("images")]
    GoodImageResponse[]? Images = null);

public sealed record GoodPrice(
    [property: JsonPropertyName("price")]
    decimal? Price,
    
    [property: JsonPropertyName("type_id")]
    string? TypeId,
    
    [property: JsonPropertyName("currency")]
    string? Currency);
