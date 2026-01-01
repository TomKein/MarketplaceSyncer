using LinqToDB;
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Models.Responses;
using WorkerService1.Data;
using WorkerService1.Data.Models;
using WorkerService1.Services;
using ApiGood = WorkerService1.BusinessRu.Models.Responses.Good;
using DbGood = WorkerService1.Data.Models.Good;

namespace WorkerService1.TestMode;

public class BatchPriceUpdateTester
{
    private readonly IPriceUpdateService _priceService;
    private readonly IBusinessRuClient _client;
    private readonly AppDbConnection _db;
    private readonly ILogger<BatchPriceUpdateTester> _logger;
    private readonly long _businessId = 1;

    public BatchPriceUpdateTester(
        IPriceUpdateService priceService,
        IBusinessRuClient client,
        AppDbConnection db,
        ILogger<BatchPriceUpdateTester> logger)
    {
        _priceService = priceService;
        _client = client;
        _db = db;
        _logger = logger;
        
        var existingBusiness = _db.Businesses.FirstOrDefault();
        if (existingBusiness != null)
        {
            _businessId = existingBusiness.Id;
        }
    }

    public async Task RunBatchTestAsync(CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("BATCH PRICE UPDATE TEST (20 goods)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        await TestBatchFetch20GoodsAsync(ct);
        await TestSinglePriceUpdateAsync(ct);

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("FINAL: good_prices TABLE DUMP");
        Console.WriteLine("=".PadRight(80, '='));
        
        var allPrices = _db.GoodPrices
            .OrderBy(p => p.Id)
            .ToList();

        Console.WriteLine($"Total records: {allPrices.Count}");
        Console.WriteLine();
        Console.WriteLine(
            "ID | GoodId | PriceTypeId | ExternalPriceRecordId | " +
            "OriginalPrice | CurrentPrice | CalculatedPrice | IsProcessed");
        Console.WriteLine("-".PadRight(128, '-'));

        foreach (var p in allPrices)
        {
            var calcPrice = p.CalculatedPrice?.ToString("F2") ?? "NULL";
            Console.WriteLine(
                $"{p.Id,3} | {p.GoodId,6} | {p.PriceTypeId,-11} | " +
                $"{p.ExternalPriceRecordId,-21} | " +
                $"{p.OriginalPrice,13:F2} | {p.CurrentPrice,12:F2} | " +
                $"{calcPrice,15} | {p.IsProcessed}");
        }

        Console.WriteLine();
        Console.WriteLine("Records with issues:");
        var issues = allPrices.Where(p => 
            p.CurrentPrice == 0 || 
            p.CalculatedPrice == null || 
            p.CalculatedPrice == 0).ToList();

        if (issues.Any())
        {
            Console.WriteLine($"Found {issues.Count} records with zero/null prices:");
            foreach (var p in issues)
            {
                Console.WriteLine(
                    $"  ID {p.Id}: Original={p.OriginalPrice:F2}, " +
                    $"Current={p.CurrentPrice:F2}, " +
                    $"Calculated={p.CalculatedPrice?.ToString("F2") ?? "NULL"}");
            }
        }
        else
        {
            Console.WriteLine("No issues found!");
        }

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("BATCH TESTS COMPLETED");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
    }

    private async Task TestBatchFetch20GoodsAsync(CancellationToken ct)
    {
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Batch Fetch 20 Goods + Prices + DB Save");
        Console.WriteLine("-".PadRight(80, '-'));

        try
        {
            ApiGood[] goods = await _priceService.GetGoodsBatchAsync(1, 20, ct);
            
            if (goods.Length == 0)
            {
                Console.WriteLine("No goods found");
                return;
            }

            Console.WriteLine($"[Goods] Fetched {goods.Length} goods");
            Console.WriteLine();

            var goodIds = goods.Select(g => g.Id).ToArray();
            
            var priceListGoods = await _priceService
                .GetPriceListGoodsForBatchAsync(goodIds, ct);
            
            Console.WriteLine(
                $"[PriceList] Found {priceListGoods.Length} connections");
            Console.WriteLine();

            var priceListGoodIds = priceListGoods
                .Select(plg => plg.Id)
                .ToArray();

            var prices = await _priceService.GetPricesForBatchAsync(
                priceListGoodIds,
                "75524",
                ct);

            Console.WriteLine($"[Prices] Found {prices.Length} prices");
            Console.WriteLine();

            var pricesByGood = prices
                .GroupBy(p => p.PriceListGoodId)
                .ToDictionary(g => g.Key, g => g.ToArray());

            int savedCount = 0;
            int updatedCount = 0;

            foreach (var good in goods)
            {
                var goodPriceListGoods = priceListGoods
                    .Where(plg => plg.GoodId == good.Id)
                    .ToArray();

                foreach (var plg in goodPriceListGoods)
                {
                    if (!pricesByGood.TryGetValue(plg.Id, out var goodPrices))
                        continue;

                    var latestPrice = _priceService
                        .GetLatestPriceByDate(goodPrices);

                    if (latestPrice == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(latestPrice.Price))
                    {
                        Console.WriteLine(
                            $"WARN: Good {good.Id} has empty price, skipping");
                        continue;
                    }

                    if (!decimal.TryParse(latestPrice.Price, out var currentPrice))
                    {
                        Console.WriteLine(
                            $"WARN: Good {good.Id} has invalid price '{latestPrice.Price}', " +
                            $"skipping");
                        continue;
                    }

                    if (currentPrice == 0)
                    {
                        Console.WriteLine(
                            $"WARN: Good {good.Id} has zero price, skipping");
                        continue;
                    }
                    
                    var calculatedPrice = _priceService
                        .CalculateIncreasedPrice(currentPrice);

                    var priceListIdStr = plg.PriceListId ?? "0";
                    if (!long.TryParse(priceListIdStr, out var priceListId))
                    {
                        priceListId = 0;
                    }

                    var existingGood = _db.Goods
                        .Where(g => g.ExternalId == good.Id)
                        .FirstOrDefault();

                    long goodDbId;
                    
                    if (existingGood == null)
                    {
                        var newGood = new DbGood
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
                        savedCount++;
                    }
                    else
                    {
                        goodDbId = existingGood.Id;
                        
                        if (existingGood.Name != good.Name)
                        {
                            existingGood.Name = good.Name ?? "";
                            existingGood.UpdatedAt = DateTimeOffset.UtcNow;
                            await DataExtensions.UpdateAsync(
                                _db,
                                existingGood);
                            updatedCount++;
                        }
                    }

                    var existingPriceList = _db.PriceLists
                        .Where(pl => pl.ExternalId == priceListIdStr)
                        .FirstOrDefault();

                    long dbPriceListId;
                    
                    if (existingPriceList != null)
                    {
                        dbPriceListId = existingPriceList.Id;
                    }
                    else
                    {
                        var anyPriceList = _db.PriceLists
                            .Where(pl => pl.BusinessId == _businessId)
                            .FirstOrDefault();
                        
                        if (anyPriceList != null)
                        {
                            dbPriceListId = anyPriceList.Id;
                        }
                        else
                        {
                            Console.WriteLine(
                                $"WARN: No price list found, skipping good {good.Id}");
                            continue;
                        }
                    }

                    var existingPrice = _db.GoodPrices
                        .Where(p => p.BusinessId == _businessId
                                    && p.GoodId == goodDbId 
                                    && p.PriceListId == dbPriceListId)
                        .FirstOrDefault();

                    if (existingPrice == null)
                    {
                        var newPrice = new GoodPrice
                        {
                            BusinessId = _businessId,
                            GoodId = goodDbId,
                            PriceListId = dbPriceListId,
                            ExternalPriceRecordId = latestPrice.Id,
                            PriceTypeId = "75524",
                            PriceListGoodId = plg.Id,
                            BusinessRuUpdatedAt = ParseDate(latestPrice.Updated),
                            OriginalPrice = currentPrice,
                            CalculatedPrice = calculatedPrice,
                            CurrentPrice = currentPrice,
                            IsProcessed = false,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                        
                        await DataExtensions.InsertAsync(_db, newPrice);
                        savedCount++;
                    }
                    else
                    {
                        var needsUpdate = false;

                        if (existingPrice.CurrentPrice != currentPrice)
                        {
                            existingPrice.CurrentPrice = currentPrice;
                            existingPrice.CalculatedPrice = calculatedPrice;
                            existingPrice.BusinessRuUpdatedAt = 
                                ParseDate(latestPrice.Updated);
                            needsUpdate = true;
                        }

                        if (string.IsNullOrWhiteSpace(existingPrice.PriceListGoodId))
                        {
                            existingPrice.PriceListGoodId = plg.Id;
                            needsUpdate = true;
                        }

                        if (string.IsNullOrWhiteSpace(existingPrice.PriceTypeId))
                        {
                            existingPrice.PriceTypeId = "75524";
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            await DataExtensions.UpdateAsync(_db, existingPrice);
                            updatedCount++;
                        }
                    }
                }
            }

            Console.WriteLine($"[DB] Saved: {savedCount}, Updated: {updatedCount}");
            Console.WriteLine();

            Console.WriteLine("Sample goods with prices:");
            var sample = goods.Take(3);
            foreach (var good in sample)
            {
                var goodPriceListGoods = priceListGoods
                    .Where(plg => plg.GoodId == good.Id)
                    .ToArray();

                Console.WriteLine($"  {good.Name} (ID: {good.Id})");
                
                foreach (var plg in goodPriceListGoods)
                {
                    if (pricesByGood.TryGetValue(plg.Id, out var goodPrices))
                    {
                        var latest = _priceService
                            .GetLatestPriceByDate(goodPrices);
                        
                        if (latest != null)
                        {
                            var price = decimal.Parse(latest.Price ?? "0");
                            var newPrice = _priceService
                                .CalculateIncreasedPrice(price);
                            
                            Console.WriteLine(
                                $"    Price: {price:F2} -> {newPrice:F2} " +
                                $"({latest.Updated})");
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("SUCCESS: Batch fetch and DB save completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Batch] Test failed");
            Console.WriteLine($"ERROR: {ex.Message}");
            throw;
        }
    }

    private async Task TestSinglePriceUpdateAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Single Price Update in Business.ru");
        Console.WriteLine("-".PadRight(80, '-'));

        try
        {
            var priceToUpdate = _db.GoodPrices
                .Where(p => !p.IsProcessed)
                .OrderBy(p => p.Id)
                .FirstOrDefault();

            if (priceToUpdate == null)
            {
                Console.WriteLine("No unprocessed prices found");
                return;
            }

            var good = _db.Goods
                .Where(g => g.Id == priceToUpdate.GoodId)
                .FirstOrDefault();

            Console.WriteLine($"[Update] Good: {good?.Name}");
            Console.WriteLine($"[Update] Current: {priceToUpdate.CurrentPrice:F2}");
            Console.WriteLine(
                $"[Update] New: {priceToUpdate.CalculatedPrice?.ToString("F2") ?? "N/A"}");
            Console.WriteLine($"[Update] PriceListGoodId: {priceToUpdate.PriceListGoodId}");
            Console.WriteLine();

            if (string.IsNullOrWhiteSpace(priceToUpdate.PriceListGoodId))
            {
                Console.WriteLine("ERROR: PriceListGoodId is empty, cannot update");
                return;
            }

            Console.WriteLine("Creating new price record in Business.ru...");
            
            var newPriceId = await _priceService.CreateNewPriceAsync(
                priceToUpdate.PriceListGoodId,
                priceToUpdate.CalculatedPrice ?? 0,
                ct);

            priceToUpdate.ExternalPriceRecordId = newPriceId;
            priceToUpdate.IsProcessed = true;
            priceToUpdate.CurrentPrice = priceToUpdate.CalculatedPrice ?? 0;
            
            await DataExtensions.UpdateAsync(_db, priceToUpdate);

            Console.WriteLine($"[Update] New price ID: {newPriceId}");
            Console.WriteLine(
                $"[Update] {priceToUpdate.OriginalPrice:F2} -> " +
                $"{priceToUpdate.CalculatedPrice?.ToString("F2") ?? "N/A"} SUCCESS");
            Console.WriteLine();

            Console.WriteLine("-".PadRight(80, '-'));
            Console.WriteLine("VERIFICATION: Check all prices for this good");
            Console.WriteLine("-".PadRight(80, '-'));

            var allPricesFromApi = await _client.GetGoodPricesAsync(
                good!.ExternalId,
                priceTypeId: "75524",
                limit: 100,
                ct);

            Console.WriteLine(
                $"Total prices in Business.ru for good {good.ExternalId}: " +
                $"{allPricesFromApi.Length}");
            Console.WriteLine();

            var grouped = allPricesFromApi
                .GroupBy(p => p.PriceListGoodId)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                Console.WriteLine($"PriceListGoodId: {group.Key}");
                var sortedPrices = group
                    .OrderByDescending(p => ParseDate(p.Updated))
                    .ToArray();

                for (int i = 0; i < sortedPrices.Length; i++)
                {
                    var p = sortedPrices[i];
                    var marker = i == 0 ? " <- LATEST" : "";
                    var isNew = p.Id == newPriceId ? " [NEW]" : "";
                    Console.WriteLine(
                        $"  [{i + 1}] ID: {p.Id}, Price: {p.Price}, " +
                        $"Updated: {p.Updated}{marker}{isNew}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("-".PadRight(80, '-'));
            Console.WriteLine("DATABASE RECORD:");
            Console.WriteLine("-".PadRight(80, '-'));

            var dbRecord = _db.GoodPrices
                .Where(p => p.Id == priceToUpdate.Id)
                .FirstOrDefault();

            if (dbRecord != null)
            {
                Console.WriteLine($"ID: {dbRecord.Id}");
                Console.WriteLine($"GoodId: {dbRecord.GoodId}");
                Console.WriteLine($"BusinessId: {dbRecord.BusinessId}");
                Console.WriteLine($"PriceListId: {dbRecord.PriceListId}");
                Console.WriteLine($"PriceListGoodId: {dbRecord.PriceListGoodId}");
                Console.WriteLine($"PriceTypeId: {dbRecord.PriceTypeId}");
                Console.WriteLine($"ExternalPriceRecordId: {dbRecord.ExternalPriceRecordId}");
                Console.WriteLine($"OriginalPrice: {dbRecord.OriginalPrice:F2}");
                Console.WriteLine($"CurrentPrice: {dbRecord.CurrentPrice:F2}");
                Console.WriteLine(
                    $"CalculatedPrice: {dbRecord.CalculatedPrice?.ToString("F2") ?? "N/A"}");
                Console.WriteLine($"IsProcessed: {dbRecord.IsProcessed}");
                Console.WriteLine(
                    $"BusinessRuUpdatedAt: {dbRecord.BusinessRuUpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
                Console.WriteLine($"CreatedAt: {dbRecord.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"UpdatedAt: {dbRecord.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                Console.WriteLine("ERROR: Record not found in database");
            }
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Update] Failed");
            Console.WriteLine($"ERROR: {ex.Message}");
            throw;
        }
    }

    private static DateTimeOffset? ParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (DateTimeOffset.TryParse(dateString, out var date))
            return date;

        return null;
    }
}
