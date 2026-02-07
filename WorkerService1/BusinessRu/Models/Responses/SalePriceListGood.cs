using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Responses;

public sealed record SalePriceListGood(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("good_id")] 
    string? GoodId = null,
    
    [property: JsonPropertyName("price_list_id")] 
    string? PriceListId = null);
