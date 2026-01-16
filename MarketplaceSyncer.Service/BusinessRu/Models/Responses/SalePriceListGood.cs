using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record SalePriceListGood(
    [property: JsonPropertyName("id")] 
    long Id,
    
    [property: JsonPropertyName("good_id")] 
    long? GoodId = null,
    
    [property: JsonPropertyName("price_list_id")] 
    long? PriceListId = null);
