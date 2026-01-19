using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record StoreResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("address")]
    string? Address = null,

    [property: JsonPropertyName("archive")]
    bool Archive = false,

    [property: JsonPropertyName("deleted")]
    bool Deleted = false,

    [property: JsonPropertyName("updated")]
    [property: JsonConverter(typeof(BusinessRu.Http.BusinessRuDateTimeConverter))]
    DateTimeOffset Updated = default,

    [property: JsonPropertyName("responsible_employee_id")]
    long? ResponsibleEmployeeId = null,

    [property: JsonPropertyName("debit_type")]
    int? DebitType = null,

    [property: JsonPropertyName("deny_negative_balance")]
    bool DenyNegativeBalance = false
);
