using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record ChargeGoodResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("charge_id")]
    long ChargeId,

    [property: JsonPropertyName("good_id")]
    long GoodId,

    [property: JsonPropertyName("amount")]
    decimal Amount,

    [property: JsonPropertyName("sum")]
    decimal? Sum = null,

    [property: JsonPropertyName("price")]
    decimal? Price = null,

    [property: JsonPropertyName("measure_id")]
    long? MeasureId = null,

    [property: JsonPropertyName("modification_id")]
    long? ModificationId = null,

    [property: JsonPropertyName("price_sale")]
    decimal? PriceSale = null,

    [property: JsonPropertyName("marking_number")]
    string[]? MarkingNumber = null,

    [property: JsonPropertyName("serial_number")]
    string[]? SerialNumber = null,
    
    [property: JsonPropertyName("updated")]
    [property: JsonConverter(typeof(BusinessRu.Http.BusinessRuDateTimeConverter))]
    DateTimeOffset Updated = default
);
