using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Requests;

public sealed record GetGoodsRequest(
    [property: JsonPropertyName("type")] 
    int? Type = null,
    
    [property: JsonPropertyName("archive")] 
    int? Archive = null,
    
    [property: JsonPropertyName("limit")] 
    int? Limit = null,
    
    [property: JsonPropertyName("page")] 
    int? Page = null,
    
    [property: JsonPropertyName("with_prices")] 
    int? WithPrices = null,
    
    [property: JsonPropertyName("type_price_ids[0]")] 
    string? TypePriceId = null);
