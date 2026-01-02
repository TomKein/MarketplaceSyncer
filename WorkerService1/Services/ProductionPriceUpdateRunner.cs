using LinqToDB;
using Microsoft.Extensions.Options;
using WorkerService1.BusinessRu.Client;
using WorkerService1.Configuration;
using WorkerService1.Data;
using WorkerService1.Data.Models;
using ApiGood = WorkerService1.BusinessRu.Models.Responses.Good;

namespace WorkerService1.Services;

public class ProductionPriceUpdateRunner
{
    private readonly IPriceUpdateService _priceService;
    private readonly IBusinessRuClient _client;
    private readonly AppDbConnection _db;
    private readonly PriceUpdateOptions _options;
    private readonly ILogger<ProductionPriceUpdateRunner> _logger;
    private readonly long _businessId;

    public ProductionPriceUpdateRunner(
        IPriceUpdateService priceService,
        IBusinessRuClient client,
        AppDbConnection db,
        IOptions<PriceUpdateOptions> options,
        ILogger<ProductionPriceUpdateRunner> logger)
    {
        _priceService = priceService;
        _client = client;
        _db = db;
        _options = options.Value;
        _logger = logger;

        var business = _db.Businesses.FirstOrDefault();
        _businessId = business?.Id ?? 1;
    }

    public async Task<UpdateStats> RunUpdateAsync(
        string sessionPriceListId,
        int? startFromPage = null,
        CancellationToken ct = default)
    {
        var stats = new UpdateStats
        {
            SessionPriceListId = sessionPriceListId,
            StartTime = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Starting production price update for all active goods");
        _logger.LogInformation("Session price list ID: {PriceListId}", 
            sessionPriceListId);

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"PRODUCTION PRICE UPDATE - Session: {sessionPriceListId}");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var page = startFromPage ?? 1;
        var hasMorePages = true;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            var batchStartTime = DateTimeOffset.UtcNow;
            
            _logger.LogInformation(
                "Processing page {Page}, batch size {BatchSize}",
                page,
                _options.BatchSize);

            Console.WriteLine(
                $"[{DateTimeOffset.UtcNow:HH:mm:ss}] " +
                $"Page {page}: Fetching goods...");

            var goods = await _priceService.GetGoodsBatchAsync(
                page,
                _options.BatchSize,
                ct);

            if (goods.Length == 0)
            {
                hasMorePages = false;
                break;
            }

            stats.TotalGoodsProcessed += goods.Length;

            Console.WriteLine(
                $"[{DateTimeOffset.UtcNow:HH:mm:ss}] " +
                $"Page {page}: Processing {goods.Length} goods...");

            var batchStats = await ProcessBatchAsync(
                goods,
                sessionPriceListId,
                ct);

            stats.PricesCreated += batchStats.PricesCreated;
            stats.GoodsSkipped += batchStats.GoodsSkipped;
            stats.Errors += batchStats.Errors;

            var batchDuration = DateTimeOffset.UtcNow - batchStartTime;

            Console.WriteLine(
                $"[{DateTimeOffset.UtcNow:HH:mm:ss}] " +
                $"Page {page} completed in {batchDuration.TotalSeconds:F1}s: " +
                $"{batchStats.PricesCreated} created, " +
                $"{batchStats.GoodsSkipped} skipped, " +
                $"{batchStats.Errors} errors");
            Console.WriteLine();

            if (goods.Length < _options.BatchSize)
            {
                hasMorePages = false;
            }

            page++;
        }

        stats.EndTime = DateTimeOffset.UtcNow;
        stats.Duration = stats.EndTime.Value - stats.StartTime;

        LogFinalStats(stats);

        return stats;
    }

    private async Task<BatchStats> ProcessBatchAsync(
        ApiGood[] goods,
        string sessionPriceListId,
        CancellationToken ct)
    {
        var stats = new BatchStats();

        var goodIds = goods.Select(g => g.Id).ToArray();

        var chunks = goodIds
            .Select((id, index) => new { id, index })
            .GroupBy(x => x.index / _options.BatchFilterSize)
            .Select(g => g.Select(x => x.id).ToArray())
            .ToArray();

        foreach (var chunk in chunks)
        {
            var priceListGoods = await _priceService
                .GetPriceListGoodsForBatchAsync(chunk, ct); // sessionPriceListId, 

            var priceListGoodIds = priceListGoods
                .Where(x => x.PriceListId != sessionPriceListId)
                .GroupBy(x => x.GoodId)
                .Select(group => group.MaxBy(x => int.Parse(x.Id!))!.Id)
                .ToArray();

            if (priceListGoodIds.Length == 0)
                continue;

            if (priceListGoodIds.Length > _options.BatchFilterSize || priceListGoodIds.Length > 50)
            {
                _logger.LogError("ups2");
            }

            var prices = await _priceService.GetPricesForBatchAsync(
                priceListGoodIds,
                _options.TargetPriceTypeId,
                ct);

            var pricesByGood = prices
                .GroupBy(p => p.PriceListGoodId)
                .ToDictionary(g => g.Key, g => g.ToArray());

            foreach (var good in goods.Where(g => chunk.Contains(g.Id)))
            {
                var result = await ProcessSingleGoodAsync(
                    good,
                    priceListGoods,
                    pricesByGood,
                    sessionPriceListId,
                    ct);

                if (result.IsSuccess)
                    stats.PricesCreated++;
                else if (result.IsSkipped)
                    stats.GoodsSkipped++;
                else if (result.IsError)
                    stats.Errors++;
            }
        }

        return stats;
    }

    private async Task<ProcessResult> ProcessSingleGoodAsync(
        ApiGood good,
        WorkerService1.BusinessRu.Models.Responses.SalePriceListGood[] allPriceListGoods,
        Dictionary<string, WorkerService1.BusinessRu.Models.Responses.SalePriceListGoodPrice[]> pricesByGood,
        string sessionPriceListId,
        CancellationToken ct)
    {
        try
        {
            var goodPriceListGoods = allPriceListGoods
                .Where(plg => plg.GoodId == good.Id)
                .ToArray();

            if (goodPriceListGoods.Length == 0)
            {
                _logger.LogDebug(
                    "Good {GoodId} has no price list connections, skipping",
                    good.Id);
                return ProcessResult.Skipped();
            }

            decimal? currentPrice = null;

            foreach (var plg in goodPriceListGoods)
            {
                if (pricesByGood.TryGetValue(plg.Id, out var goodPrices))
                {
                    var latestPrice = _priceService.GetLatestPriceByDate(goodPrices);
                    if (latestPrice != null && 
                        decimal.TryParse(latestPrice.Price, out var price) && 
                        price > 0)
                    {
                        currentPrice = price;
                        break;
                    }
                }
            }

            if (!currentPrice.HasValue || currentPrice.Value == 0)
            {
                _logger.LogDebug(
                    "Good {GoodId} has no valid price, skipping",
                    good.Id);
                return ProcessResult.Skipped();
            }

            var calculatedPrice = _priceService.CalculateIncreasedPrice(
                currentPrice.Value);

            var existingGood = _db.Goods
                .Where(g => g.ExternalId == good.Id)
                .FirstOrDefault();

            long goodDbId;

            if (existingGood == null)
            {
                var newGood = new Good
                {
                    ExternalId = good.Id,
                    Name = good.Name ?? "",
                    PartNumber = good.PartNumber ?? "",
                    StoreCode = good.StoreCode,
                    BusinessId = _businessId,
                    Archive = good.Archive,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                goodDbId = await _db.InsertWithInt64IdentityAsync(newGood);
            }
            else
            {
                goodDbId = existingGood.Id;
            }

            var sessionPriceListDbId = GetOrCreatePriceListDbId(sessionPriceListId);

            var existingPrice = _db.GoodPrices
                .Where(p => p.BusinessId == _businessId
                            && p.GoodId == goodDbId
                            && p.PriceListId == sessionPriceListDbId)
                .FirstOrDefault();

            if (existingPrice != null && existingPrice.IsProcessed)
            {
                _logger.LogDebug(
                    "Good {GoodId} already processed, skipping",
                    good.Id);
                return ProcessResult.Skipped();
            }

            var priceListGoodId = await _priceService.AddGoodToPriceListAsync(
                sessionPriceListId,
                good.Id,
                ct);

            var newPriceId = await _priceService.CreateNewPriceAsync(
                priceListGoodId,
                calculatedPrice,
                ct);

            if (existingPrice == null)
            {
                var newPrice = new GoodPrice
                {
                    BusinessId = _businessId,
                    GoodId = goodDbId,
                    PriceListId = sessionPriceListDbId,
                    ExternalPriceRecordId = newPriceId,
                    PriceTypeId = _options.TargetPriceTypeId,
                    PriceListGoodId = priceListGoodId,
                    BusinessRuUpdatedAt = DateTimeOffset.UtcNow,
                    OriginalPrice = currentPrice.Value,
                    CalculatedPrice = calculatedPrice,
                    CurrentPrice = calculatedPrice,
                    IsProcessed = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                await DataExtensions.InsertAsync(_db, newPrice);
            }
            else
            {
                existingPrice.ExternalPriceRecordId = newPriceId;
                existingPrice.PriceListGoodId = priceListGoodId;
                existingPrice.CurrentPrice = calculatedPrice;
                existingPrice.CalculatedPrice = calculatedPrice;
                existingPrice.IsProcessed = true;
                existingPrice.UpdatedAt = DateTimeOffset.UtcNow;
                existingPrice.BusinessRuUpdatedAt = DateTimeOffset.UtcNow;

                await DataExtensions.UpdateAsync(_db, existingPrice);
            }

            return ProcessResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing good {GoodId}: {Message}",
                good.Id,
                ex.Message);
            return ProcessResult.Error();
        }
    }

    private long GetOrCreatePriceListDbId(string externalId)
    {
        var existing = _db.PriceLists
            .Where(pl => pl.ExternalId == externalId)
            .FirstOrDefault();

        if (existing != null)
            return existing.Id;

        var newPriceList = new PriceList
        {
            BusinessId = _businessId,
            ExternalId = externalId,
            Name = $"Session {externalId}",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return _db.InsertWithInt64Identity(newPriceList);
    }

    private void LogFinalStats(UpdateStats stats)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("UPDATE COMPLETED");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"Session Price List: {stats.SessionPriceListId}");
        Console.WriteLine($"Total goods processed: {stats.TotalGoodsProcessed}");
        Console.WriteLine($"Prices created: {stats.PricesCreated}");
        Console.WriteLine($"Goods skipped: {stats.GoodsSkipped}");
        Console.WriteLine($"Errors: {stats.Errors}");
        Console.WriteLine($"Duration: {stats.Duration.TotalMinutes:F1} minutes");
        Console.WriteLine();

        _logger.LogInformation(
            "Production update completed: " +
            "{Total} goods, {Created} prices, {Skipped} skipped, {Errors} errors",
            stats.TotalGoodsProcessed,
            stats.PricesCreated,
            stats.GoodsSkipped,
            stats.Errors);
    }
}

public class UpdateStats
{
    public string SessionPriceListId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalGoodsProcessed { get; set; }
    public int PricesCreated { get; set; }
    public int GoodsSkipped { get; set; }
    public int Errors { get; set; }
}

public class BatchStats
{
    public int PricesCreated { get; set; }
    public int GoodsSkipped { get; set; }
    public int Errors { get; set; }
}

public enum ProcessStatus
{
    Success,
    Skipped,
    Error
}

public class ProcessResult
{
    public ProcessStatus Status { get; private set; }

    // Удобные свойства для проверки
    public bool IsSuccess => Status == ProcessStatus.Success;
    public bool IsSkipped => Status == ProcessStatus.Skipped;
    public bool IsError => Status == ProcessStatus.Error;

    public static ProcessResult Success() => new() { Status = ProcessStatus.Success };
    public static ProcessResult Skipped() => new() { Status = ProcessStatus.Skipped };
    public static ProcessResult Error() => new() { Status = ProcessStatus.Error };
}
