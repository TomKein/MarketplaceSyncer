using MarketplaceSyncer.Service.BusinessRu.Http;
using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Ответ API Business.ru для текущих цен товаров (currentprices).
/// </summary>
public sealed record CurrentPriceResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("good_id")]
    long GoodId,

    [property: JsonPropertyName("modification_id")]
    long? ModificationId = null,

    [property: JsonPropertyName("measure_id")]
    long? MeasureId = null,

    [property: JsonPropertyName("price_type_id")]
    long PriceTypeId = 0,

    [property: JsonPropertyName("price")]
    decimal Price = 0,

    [property: JsonPropertyName("is_base_measure")]
    bool IsBaseMeasure = false,

    [property: JsonPropertyName("updated")]
    [property: JsonConverter(typeof(BusinessRuDateTimeConverter))]
    DateTimeOffset? Updated = null
);
