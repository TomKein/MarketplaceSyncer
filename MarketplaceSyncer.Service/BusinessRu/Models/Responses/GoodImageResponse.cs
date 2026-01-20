using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Image attached to a good in Business.ru
/// </summary>
public sealed record GoodImageResponse(
    [property: JsonPropertyName("id")] long? Id = null,
    [property: JsonPropertyName("good_id")] long? GoodId = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("url")] string? Url = null,
    
    [property: JsonPropertyName("sort")]
    int? Sort = null,

    [property: JsonPropertyName("time_create")]
    [property: JsonConverter(typeof(BusinessRu.Http.BusinessRuDateTimeConverter))]
    DateTimeOffset? TimeCreate = null
);
