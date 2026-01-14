using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record SalePriceListGoodPrice(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("price_list_good_id")] 
    string? PriceListGoodId = null,
    
    [property: JsonPropertyName("price_type_id")] 
    string? PriceTypeId = null,
    
    [property: JsonPropertyName("price")] 
    string? Price = null,
    
    [property: JsonPropertyName("updated")] 
    string? Updated = null);
