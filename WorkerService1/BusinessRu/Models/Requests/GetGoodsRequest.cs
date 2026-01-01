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
    int? Page = null);
