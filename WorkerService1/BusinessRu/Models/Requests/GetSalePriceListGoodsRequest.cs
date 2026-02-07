using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Requests;

public sealed record GetSalePriceListGoodsRequest(
    [property: JsonPropertyName("limit")] 
    int? Limit = null,
    
    [property: JsonPropertyName("offset")] 
    int? Offset = null);
