using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Requests;

public sealed record ChargeRequest
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("date")]
    [JsonConverter(typeof(BusinessRu.Http.BusinessRuDateTimeConverter))]
    public DateTimeOffset? Date { get; init; }

    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; init; }

    [JsonPropertyName("store_id")]
    public long? StoreId { get; init; }

    [JsonPropertyName("author_employee_id")]
    public long? AuthorEmployeeId { get; init; }

    [JsonPropertyName("responsible_employee_id")]
    public long? ResponsibleEmployeeId { get; init; }

    [JsonPropertyName("inventory_id")]
    public long? InventoryId { get; init; }

    [JsonPropertyName("held")]
    public bool? Held { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
    
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("goods")]
    public IEnumerable<ChargeGoodRequest>? Goods { get; init; }
}
