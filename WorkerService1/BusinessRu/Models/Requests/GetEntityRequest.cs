using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Requests;

public sealed record GetEntityRequest(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("fields")] 
    string[]? Fields = null);
