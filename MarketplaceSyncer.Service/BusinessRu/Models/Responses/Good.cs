using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record Good(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("name")] 
    string? Name = null,
    
    [property: JsonPropertyName("part_number")] 
    string? PartNumber = null,
    
    [property: JsonPropertyName("store_code")] 
    string? StoreCode = null,
    
    [property: JsonPropertyName("archive")] 
    bool Archive = false,
    
    [property: JsonPropertyName("prices")] 
    SalePriceListGoodPrice[]? Prices = null);
