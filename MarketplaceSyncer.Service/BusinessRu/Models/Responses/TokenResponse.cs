using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record TokenResponse(
    [property: JsonPropertyName("token")] 
    string? Token);
