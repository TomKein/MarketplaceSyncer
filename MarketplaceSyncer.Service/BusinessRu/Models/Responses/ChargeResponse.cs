using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record ChargeResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("date")]
    [property: JsonConverter(typeof(BusinessRu.Http.BusinessRuDateTimeConverter))]
    DateTimeOffset Date,

    [property: JsonPropertyName("number")]
    string Number,

    [property: JsonPropertyName("organization_id")]
    long OrganizationId,

    [property: JsonPropertyName("store_id")]
    long StoreId,

    [property: JsonPropertyName("author_employee_id")]
    long? AuthorEmployeeId = null,

    [property: JsonPropertyName("responsible_employee_id")]
    long? ResponsibleEmployeeId = null,

    [property: JsonPropertyName("inventory_id")]
    long? InventoryId = null,

    [property: JsonPropertyName("held")]
    bool Held = false,

    [property: JsonPropertyName("sum")]
    decimal? Sum = null,

    [property: JsonPropertyName("comment")]
    string? Comment = null,

    [property: JsonPropertyName("reason")]
    string? Reason = null,

    [property: JsonPropertyName("departments_ids")]
    long[]? DepartmentsIds = null,

    [property: JsonPropertyName("goods")]
    ChargeGoodResponse[]? Goods = null,

    [property: JsonPropertyName("updated")]
    [property: JsonConverter(typeof(BusinessRu.Http.BusinessRuDateTimeConverter))]
    DateTimeOffset Updated = default,

    [property: JsonPropertyName("deleted")]
    bool Deleted = false
);
