using WorkerService1.BusinessRu.Models.Responses;

namespace WorkerService1.Services;

public interface IPriceUpdateService
{
    Task<Good[]> GetGoodsBatchAsync(
        int page,
        int limit,
        string? priceTypeId = null,
        CancellationToken cancellationToken = default);

    [Obsolete("No longer needed - prices are fetched directly with goods using with_prices parameter")]
    Task<SalePriceListGood[]> GetPriceListGoodsForBatchAsync(
        //string priceListId,
        string[] goodIds,
        CancellationToken cancellationToken = default);

    [Obsolete("No longer needed - prices are fetched directly with goods using with_prices parameter")]
    Task<SalePriceListGoodPrice[]> GetPricesForBatchAsync(
        string[] priceListGoodIds,
        string priceTypeId,
        CancellationToken cancellationToken = default);

    SalePriceListGoodPrice? GetLatestPriceByDate(
        SalePriceListGoodPrice[] prices);

    decimal CalculateIncreasedPrice(decimal currentPrice);

    Task<string> CreateNewPriceAsync(
        string priceListGoodId,
        decimal newPrice,
        CancellationToken cancellationToken = default);

    Task<string> CreateUpdateSessionPriceListAsync(
        string sessionName,
        CancellationToken cancellationToken = default);

    Task<string> AddGoodToPriceListAsync(
        string priceListId,
        string goodId,
        CancellationToken cancellationToken = default);
}
