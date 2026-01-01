using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Requests;

public sealed record GetSalePriceTypesRequest(
    [property: JsonPropertyName("limit")] 
    int? Limit = null,
    
    [property: JsonPropertyName("offset")] 
    int? Offset = null);
