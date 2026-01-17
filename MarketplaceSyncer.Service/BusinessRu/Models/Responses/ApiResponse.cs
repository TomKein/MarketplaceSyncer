using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record ApiResponse<T>(
    [property: JsonPropertyName("result")] 
    T? Result,
    
    [property: JsonPropertyName("request_count")]
    int RequestCount = 0,

    [property: JsonPropertyName("status")] 
    string? Status = null,
    
    [property: JsonPropertyName("error_code")] 
    string? ErrorCode = null,
    
    [property: JsonPropertyName("error_text")] 
    string? ErrorText = null
);
