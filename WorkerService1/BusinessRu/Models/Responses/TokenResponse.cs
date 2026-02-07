using System.Text.Json.Serialization;

namespace WorkerService1.BusinessRu.Models.Responses;

public sealed record TokenResponse(
    [property: JsonPropertyName("token")] 
    string? Token);
