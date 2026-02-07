using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Requests;

public sealed record UpdatePriceRequest(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("price")] 
    string Price);
