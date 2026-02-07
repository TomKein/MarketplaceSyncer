using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Requests;

public sealed record GetSalePriceListsRequest(
    [property: JsonPropertyName("limit")] 
    int? Limit = null);
