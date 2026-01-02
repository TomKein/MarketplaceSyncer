using Microsoft.Extensions.Options;
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Models.Responses;
using WorkerService1.Configuration;

namespace WorkerService1.Services;

public class PriceUpdateService : IPriceUpdateService
{
    private readonly IBusinessRuClient _client;
    private readonly PriceUpdateOptions _options;
    private readonly ILogger<PriceUpdateService> _logger;

    public PriceUpdateService(
        IBusinessRuClient client,
        IOptions<PriceUpdateOptions> options,
        ILogger<PriceUpdateService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Good[]> GetGoodsBatchAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Batch] Fetching page {Page}, limit {Limit}",
            page,
            limit);

        var request = new Dictionary<string, string>
        {
            ["archive"] = "0",
            ["type"] = "1",
            ["limit"] = limit.ToString(),
            ["page"] = page.ToString()
        };

        var goods = await _client.RequestAsync<
            Dictionary<string, string>,
            Good[]>(
            HttpMethod.Get,
            "goods",
            request,
            cancellationToken);

        _logger.LogInformation(
            "[Batch] Fetched {Count} goods",
            goods?.Length ?? 0);

        return goods ?? Array.Empty<Good>();
    }

    public async Task<SalePriceListGood[]> GetPriceListGoodsForBatchAsync(
        // string priceListId,
        string[] goodIds,
        CancellationToken cancellationToken = default)
    {
        if (goodIds.Length == 0)
            return Array.Empty<SalePriceListGood>();

        _logger.LogDebug(
            "[PriceList] Fetching for {Count} goods",
            goodIds.Length);

        var request = new Dictionary<string, string>();
        
        // request["price_list_id"] = priceListId;
        // request["good_id"] = string.Join(",", goodIds);//$"[{string.Join(",", goodIds)}]";
        
        for (int i = 0; i < goodIds.Length; i++)
        {
            request[$"good_id[{i}]"] = goodIds[i];
        }
        //есть список вот таких рекордов у которых для каждого good_id, может быть несколько 

        var priceListGoods = await _client.RequestAsync<
            Dictionary<string, string>,
            SalePriceListGood[]>(
            HttpMethod.Get,
            "salepricelistgoods",
            request,
            cancellationToken);

        _logger.LogDebug(
            "[PriceList] Found {Count} connections",
            priceListGoods?.Length ?? 0);

        return priceListGoods ?? Array.Empty<SalePriceListGood>();
    }

    public async Task<SalePriceListGoodPrice[]> GetPricesForBatchAsync(
        string[] priceListGoodIds,
        string priceTypeId,
        CancellationToken cancellationToken = default)
    {
        if (priceListGoodIds.Length == 0)
            return Array.Empty<SalePriceListGoodPrice>();

        _logger.LogDebug(
            "[Prices] Fetching for {Count} price list goods",
            priceListGoodIds.Length);

        var request = new Dictionary<string, string>
        {
            ["price_type_id"] = priceTypeId
        };
        
        for (int i = 0; i < priceListGoodIds.Length; i++)
        {
            request[$"price_list_good_id[{i}]"] = priceListGoodIds[i];
        }

        var prices = await _client.RequestAsync<
            Dictionary<string, string>,
            SalePriceListGoodPrice[]>(
            HttpMethod.Get,
            "salepricelistgoodprices",
            request,
            cancellationToken);

        _logger.LogDebug(
            "[Prices] Found {Count} prices",
            prices?.Length ?? 0);

        return prices ?? Array.Empty<SalePriceListGoodPrice>();
    }

    public SalePriceListGoodPrice? GetLatestPriceByDate(
        SalePriceListGoodPrice[] prices)
    {
        if (prices.Length == 0)
            return null;

        return prices
            .OrderByDescending(p => ParseDate(p.Updated))
            .FirstOrDefault();
    }

    public decimal CalculateIncreasedPrice(decimal currentPrice)
    {
        var increased = currentPrice * (1 + _options.IncreasePercentage / 100m);
        var rounded = Math.Ceiling(increased / _options.RoundToNearest) 
                      * _options.RoundToNearest;
        return rounded;
    }

    public async Task<string> CreateNewPriceAsync(
        string priceListGoodId,
        decimal newPrice,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Update] Creating new price {Price} for {Id}",
            newPrice,
            priceListGoodId);

        var request = new Dictionary<string, string>
        {
            ["price_list_good_id"] = priceListGoodId,
            ["price_type_id"] = _options.TargetPriceTypeId,
            ["price"] = newPrice.ToString("0.00", 
                System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await _client.RequestAsync<
            Dictionary<string, string>,
            SalePriceListGoodPrice>(
            HttpMethod.Post,
            "salepricelistgoodprices",
            request,
            cancellationToken);

        _logger.LogInformation(
            "[Update] Created price ID: {Id}",
            response.Id);

        return response.Id;
    }

    public async Task<string> CreateUpdateSessionPriceListAsync(
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        var priceListName = $"Price Update {sessionName} - " +
                           $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}";

        _logger.LogInformation(
            "Creating update session price list: {Name}",
            priceListName);

        var priceListId = await _client.CreatePriceListAsync(
            priceListName,
            _options.TargetPriceTypeId,
            cancellationToken);

        _logger.LogInformation(
            "Update session price list created: {Id}",
            priceListId);

        return priceListId;
    }

    public async Task<string> AddGoodToPriceListAsync(
        string priceListId,
        string goodId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(priceListId);
        ArgumentException.ThrowIfNullOrWhiteSpace(goodId);

        _logger.LogDebug(
            "Adding good {GoodId} to price list {PriceListId}",
            goodId,
            priceListId);

        var request = new Dictionary<string, string>
        {
            ["price_list_id"] = priceListId,
            ["good_id"] = goodId
        };

        var response = await _client.RequestAsync<
            Dictionary<string, string>,
            SalePriceListGood>(
            HttpMethod.Post,
            "salepricelistgoods",
            request,
            cancellationToken);

        _logger.LogDebug(
            "Good added to price list: {PriceListGoodId}",
            response.Id);

        return response.Id;
    }

    private static DateTimeOffset ParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return DateTimeOffset.MinValue;

        if (DateTimeOffset.TryParse(dateString, out var date))
            return date;

        return DateTimeOffset.MinValue;
    }
}
