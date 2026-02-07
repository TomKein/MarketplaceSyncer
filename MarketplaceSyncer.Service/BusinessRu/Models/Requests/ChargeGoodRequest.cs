using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Requests;

public sealed record ChargeGoodRequest
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("charge_id")]
    public long ChargeId { get; init; }

    [JsonPropertyName("good_id")]
    public long GoodId { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    [JsonPropertyName("measure_id")]
    public long? MeasureId { get; init; }

    [JsonPropertyName("modification_id")]
    public long? ModificationId { get; init; }
}
