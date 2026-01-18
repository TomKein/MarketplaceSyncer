using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public record GroupResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent_id")] long? ParentId,
    [property: JsonPropertyName("deleted")] bool IsDeleted,
    [property: JsonPropertyName("default_order")] int? DefaultOrder,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("images")] System.Text.Json.JsonElement? Images,
    [property: JsonPropertyName("updated")] DateTimeOffset? Updated
);
