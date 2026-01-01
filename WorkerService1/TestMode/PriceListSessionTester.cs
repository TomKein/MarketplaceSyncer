using WorkerService1.BusinessRu.Client;
using WorkerService1.Services;

namespace WorkerService1.TestMode;

public class PriceListSessionTester
{
    private readonly IPriceUpdateService _priceService;
    private readonly IBusinessRuClient _client;
    private readonly ILogger<PriceListSessionTester> _logger;

    public PriceListSessionTester(
        IPriceUpdateService priceService,
        IBusinessRuClient client,
        ILogger<PriceListSessionTester> logger)
    {
        _priceService = priceService;
        _client = client;
        _logger = logger;
    }

    public async Task RunTestAsync(CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("PRICE LIST SESSION TEST");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        try
        {
            Console.WriteLine("Step 1: Create new price list for update session");
            Console.WriteLine("-".PadRight(80, '-'));
            
            var sessionPriceListId = await _priceService
                .CreateUpdateSessionPriceListAsync("Test Session", ct);

            Console.WriteLine($"[Success] Price list created: {sessionPriceListId}");
            Console.WriteLine();

            Console.WriteLine("Step 2: Get first good from catalog");
            Console.WriteLine("-".PadRight(80, '-'));

            var goods = await _priceService.GetGoodsBatchAsync(1, 1, ct);
            
            if (goods.Length == 0)
            {
                Console.WriteLine("ERROR: No goods found");
                return;
            }

            var testGood = goods[0];
            Console.WriteLine($"[Success] Good: {testGood.Name} (ID: {testGood.Id})");
            Console.WriteLine();

            Console.WriteLine("Step 3: Add good to new price list");
            Console.WriteLine("-".PadRight(80, '-'));

            var priceListGoodId = await _priceService
                .AddGoodToPriceListAsync(sessionPriceListId, testGood.Id, ct);

            Console.WriteLine(
                $"[Success] Good added to price list: {priceListGoodId}");
            Console.WriteLine();

            Console.WriteLine("Step 4: Create price for good in new price list");
            Console.WriteLine("-".PadRight(80, '-'));

            var testPrice = 1000m;
            Console.WriteLine($"Creating price: {testPrice:F2}");

            var priceId = await _priceService.CreateNewPriceAsync(
                priceListGoodId,
                testPrice,
                ct);

            Console.WriteLine($"[Success] Price created: ID={priceId}");
            Console.WriteLine();

            Console.WriteLine("Step 5: Verify created price");
            Console.WriteLine("-".PadRight(80, '-'));

            var allPrices = await _client.GetGoodPricesAsync(
                testGood.Id,
                priceTypeId: "75524",
                limit: 100,
                ct);

            Console.WriteLine(
                $"Total prices for good {testGood.Id}: {allPrices.Length}");

            var grouped = allPrices
                .GroupBy(p => p.PriceListGoodId)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                Console.WriteLine($"  PriceListGoodId: {group.Key}");
                foreach (var p in group.OrderByDescending(
                    p => ParseDate(p.Updated)))
                {
                    var marker = p.Id == priceId ? " [NEW]" : "";
                    Console.WriteLine(
                        $"    ID: {p.Id}, Price: {p.Price}, " +
                        $"Updated: {p.Updated}{marker}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("TEST COMPLETED SUCCESSFULLY");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  - Created price list: {sessionPriceListId}");
            Console.WriteLine($"  - Added good: {testGood.Id}");
            Console.WriteLine($"  - Created price: {priceId} = {testPrice:F2}");
            Console.WriteLine();
            Console.WriteLine(
                "This price list can now be used for all 28k goods update!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Price list session test failed");
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
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
