using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Models.Responses;

namespace WorkerService1.TestMode;

public class ApiTester
{
    private readonly IBusinessRuClient _client;
    private readonly ILogger<ApiTester> _logger;

    public ApiTester(IBusinessRuClient client, ILogger<ApiTester> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task RunAllTestsAsync(CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("API TESTING MODE");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        await TestGetGoodByIdAsync(ct);
        await TestGetGoodPriceByIdAsync(ct);

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ALL TESTS COMPLETED");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
    }

    private async Task TestGetGoodByIdAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Get Good By ID (from list)");
        Console.WriteLine("-".PadRight(80, '-'));

        try
        {
            _logger.LogInformation("Fetching first good from list");

            var listRequest = new Dictionary<string, string>
            {
                ["archive"] = "0",
                ["type"] = "1",
                ["limit"] = "1",
                ["page"] = "1"
            };

            var goodsList = await _client.RequestAsync<
                Dictionary<string, string>,
                Good[]>(
                HttpMethod.Get,
                "goods",
                listRequest,
                ct);

            Console.WriteLine("RAW RESPONSE (goods list):");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                goodsList,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            Console.WriteLine();

            if (goodsList == null || goodsList.Length == 0)
            {
                Console.WriteLine("WARNING: No goods found in list");
                return;
            }

            var good = goodsList[0];

            _logger.LogInformation(
                "Good found: ID={Id}, Name={Name}, PartNumber={PartNumber}",
                good.Id,
                good.Name,
                good.PartNumber);

            Console.WriteLine($"SUCCESS: Retrieved good from list");
            Console.WriteLine($"  ID: {good.Id}");
            Console.WriteLine($"  Name: {good.Name}");
            Console.WriteLine($"  Part Number: {good.PartNumber}");
            Console.WriteLine($"  Store Code: {good.StoreCode}");
            Console.WriteLine($"  Archive: {good.Archive}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get good by ID");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    private async Task TestGetGoodPriceByIdAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Get Price By Good ID");
        Console.WriteLine("-".PadRight(80, '-'));

        try
        {
            _logger.LogInformation("Fetching first good to get its ID");

            var listRequest = new Dictionary<string, string>
            {
                ["archive"] = "0",
                ["type"] = "1",
                ["limit"] = "1",
                ["page"] = "1"
            };

            var goodsList = await _client.RequestAsync<
                Dictionary<string, string>,
                Good[]>(
                HttpMethod.Get,
                "goods",
                listRequest,
                ct);

            if (goodsList == null || goodsList.Length == 0)
            {
                Console.WriteLine("WARNING: No goods found for price test");
                return;
            }

            var goodId = goodsList[0].Id;
            var goodName = goodsList[0].Name;
            
            Console.WriteLine($"Good ID: {goodId}");
            Console.WriteLine($"Good Name: {goodName}");
            Console.WriteLine();

            _logger.LogInformation(
                "Requesting prices for good ID: {GoodId}",
                goodId);

            var prices = await _client.GetGoodPricesAsync(goodId, limit: 10, ct);

            Console.WriteLine("RAW RESPONSE (prices):");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                prices,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            Console.WriteLine();

            if (prices == null || prices.Length == 0)
            {
                Console.WriteLine($"INFO: No prices found for good {goodId}");
                return;
            }

            _logger.LogInformation(
                "Found {Count} price entries for good {GoodId} ({GoodName})",
                prices.Length,
                goodId,
                goodName);

            Console.WriteLine($"SUCCESS: Found {prices.Length} price(s) for good '{goodName}'");
            for (int i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                Console.WriteLine($"  [{i + 1}] Price ID: {price.Id}");
                Console.WriteLine($"      Price: {price.Price}");
                Console.WriteLine($"      Price List Good ID: {price.PriceListGoodId}");
                Console.WriteLine($"      Price Type ID: {price.PriceTypeId}");
                Console.WriteLine($"      Updated: {price.Updated}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prices by good ID");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
