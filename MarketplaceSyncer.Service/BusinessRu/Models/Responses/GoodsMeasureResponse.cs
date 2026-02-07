using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

/// <summary>
/// Привязка единицы измерения к товару (goodsmeasures).
/// </summary>
public sealed record GoodsMeasureResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("good_id")] long GoodId,
    [property: JsonPropertyName("measure_id")] long MeasureId,
    [property: JsonPropertyName("base")] bool Base,
    [property: JsonPropertyName("coefficient")] decimal Coefficient, // Docs say float, using decimal for precision
    [property: JsonPropertyName("marking_pack")] bool MarkingPack,
    [property: JsonPropertyName("updated")] DateTimeOffset? Updated
);
