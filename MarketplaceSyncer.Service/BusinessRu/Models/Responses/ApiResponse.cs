using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record ApiResponse<T>(
    [property: JsonPropertyName("result")] 
    T? Result,
    
    [property: JsonPropertyName("request_count")] 
    int RequestCount = 0);
