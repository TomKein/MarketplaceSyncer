using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record SalePriceListGoodPrice(
    [property: JsonPropertyName("id")] 
    long Id,
    
    [property: JsonPropertyName("price_list_good_id")] 
    long? PriceListGoodId = null,
    
    [property: JsonPropertyName("price_type_id")] 
    long? PriceTypeId = null,
    
    [property: JsonPropertyName("price")] 
    string? Price = null,
    
    [property: JsonPropertyName("updated")] 
    string? Updated = null);
