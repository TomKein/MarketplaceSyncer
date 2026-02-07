using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Requests;

public class CommentRequest
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Id { get; set; }

    [JsonPropertyName("employee_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? AuthorEmployeeId { get; set; }

    [JsonPropertyName("document_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ModelId { get; set; }

    [JsonPropertyName("model_name")]
    public string ModelName { get; init; } = "goods";

    [JsonPropertyName("note")]
    public required string Note { get; set; }
}
