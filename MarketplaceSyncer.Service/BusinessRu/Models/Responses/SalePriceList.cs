using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record SalePriceList(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("name")] 
    string? Name = null,
    
    [property: JsonPropertyName("price_type_id")] 
    string? PriceTypeId = null,
    
    [property: JsonPropertyName("active")] 
    bool Active = false);
