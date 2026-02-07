using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Responses;

public sealed record CountResponse(
    [property: JsonPropertyName("count")] 
    int Count,
    
    [property: JsonPropertyName("request_count")] 
    int RequestCount = 0);
