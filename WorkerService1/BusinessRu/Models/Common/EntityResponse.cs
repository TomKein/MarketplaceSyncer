using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Common;

public sealed record EntityResponse(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("name")] 
    string Name,
    
    [property: JsonPropertyName("created_at")] 
    DateTime CreatedAt);
