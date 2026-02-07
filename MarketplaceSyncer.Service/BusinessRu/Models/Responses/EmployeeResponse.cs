using System.Text.Json.Serialization;
using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record EmployeeResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("last_name")]
    string? LastName = null,

    [property: JsonPropertyName("first_name")]
    string? FirstName = null,

    [property: JsonPropertyName("middle_name")]
    string? MiddleName = null,

    [property: JsonPropertyName("email")]
    string? Email = null,

    [property: JsonPropertyName("active")]
    bool Active = false,

    [property: JsonPropertyName("active_retail")]
    bool ActiveRetail = false,

    [property: JsonPropertyName("department_id")]
    long? DepartmentId = null,

    [property: JsonPropertyName("role_id")]
    long? RoleId = null,

    [property: JsonPropertyName("birthday")]
    [property: JsonConverter(typeof(BusinessRuDateTimeConverter))]
    DateTimeOffset? Birthday = null,

    [property: JsonPropertyName("phoneip")]
    string? PhoneIp = null,

    [property: JsonPropertyName("sex")]
    int? Sex = null,

    [property: JsonPropertyName("inn")]
    long? Inn = null,

    [property: JsonPropertyName("archived")]
    bool Archived = false,

    [property: JsonPropertyName("last_date_online")]
    [property: JsonConverter(typeof(BusinessRuDateTimeConverter))]
    DateTimeOffset? LastDateOnline = null,

    [property: JsonPropertyName("bid")]
    string? Bid = null,

    [property: JsonPropertyName("email_confirmed")]
    bool EmailConfirmed = false,

    [property: JsonPropertyName("updated")]
    [property: JsonConverter(typeof(BusinessRuDateTimeConverter))]
    DateTimeOffset Updated = default,

    [property: JsonPropertyName("deleted")]
    bool Deleted = false
);
