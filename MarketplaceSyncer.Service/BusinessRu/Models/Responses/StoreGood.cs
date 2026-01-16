using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record StoreGood(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("store_id")]
    long StoreId,

    [property: JsonPropertyName("good_id")]
    long GoodId,

    [property: JsonPropertyName("modification_id")]
    long? ModificationId = null,

    [property: JsonPropertyName("amount")]
    decimal Amount = 0,

    [property: JsonPropertyName("reserved")]
    decimal Reserved = 0,

    [property: JsonPropertyName("remains_min")]
    decimal? RemainsMin = null,

    [property: JsonPropertyName("updated")]
    DateTimeOffset Updated = default
);
