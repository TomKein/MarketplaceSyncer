using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public record CommentResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("document_id")] long DocumentId,
    [property: JsonPropertyName("employee_id")] long AuthorEmployeeId,
    [property: JsonPropertyName("time_create")] DateTimeOffset? TimeCreate,
    [property: JsonPropertyName("updated")] DateTimeOffset? Updated,
    [property: JsonPropertyName("note")] string Note);
