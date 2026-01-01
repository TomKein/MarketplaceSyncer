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

        await TestCountGoodsAsync(ct);
        await TestGetSingleGoodAsync(ct);
        await TestGetGoodPriceAsync(ct);

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ALL TESTS COMPLETED");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
    }

    private async Task TestCountGoodsAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Count Goods");
        Console.WriteLine("-".PadRight(80, '-'));

        try
        {
            _logger.LogInformation("Requesting goods count (archive=0, type=1)");

            var request = new Dictionary<string, string>
            {
                ["archive"] = "0",
                ["type"] = "1",
                ["count_only"] = "1"
            };

            var response = await _client.RequestAsync<
                Dictionary<string, string>,
                CountResponse>(
                HttpMethod.Get,
                "goods",
                request,
                ct);

            Console.WriteLine("RAW RESPONSE:");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                response,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            Console.WriteLine();

            _logger.LogInformation(
                "Count result: {Count} goods found",
                response.Count);

            Console.WriteLine($"SUCCESS: Found {response.Count} active goods");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count goods");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    private async Task TestGetSingleGoodAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Get Single Good");
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

            if (goodsList == null || goodsList.Length == 0)
            {
                Console.WriteLine("WARNING: No goods found in list");
                return;
            }

            var firstGood = goodsList[0];
            Console.WriteLine($"Found good ID: {firstGood.Id}");
            Console.WriteLine();

            _logger.LogInformation(
                "Requesting detailed info for good ID: {GoodId}",
                firstGood.Id);

            var detailRequest = new Dictionary<string, string>
            {
                ["id"] = firstGood.Id
            };

            var goodDetail = await _client.RequestAsync<
                Dictionary<string, string>,
                Good>(
                HttpMethod.Get,
                "good",
                detailRequest,
                ct);

            Console.WriteLine("RAW RESPONSE:");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                goodDetail,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            Console.WriteLine();

            _logger.LogInformation(
                "Good details: ID={Id}, Name={Name}, PartNumber={PartNumber}",
                goodDetail.Id,
                goodDetail.Name,
                goodDetail.PartNumber);

            Console.WriteLine($"SUCCESS: Retrieved good '{goodDetail.Name}'");
            Console.WriteLine($"  ID: {goodDetail.Id}");
            Console.WriteLine($"  Part Number: {goodDetail.PartNumber}");
            Console.WriteLine($"  Store Code: {goodDetail.StoreCode}");
            Console.WriteLine($"  Archive: {goodDetail.Archive}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get single good");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    private async Task TestGetGoodPriceAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine("TEST: Get Good Price");
        Console.WriteLine("-".PadRight(80, '-'));

        try
        {
            _logger.LogInformation("Fetching first good for price test");

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
            Console.WriteLine($"Testing price for good ID: {goodId}");
            Console.WriteLine();

            _logger.LogInformation(
                "Requesting prices for good ID: {GoodId}",
                goodId);

            var priceRequest = new Dictionary<string, string>
            {
                ["good_id"] = goodId,
                ["limit"] = "10"
            };

            var prices = await _client.RequestAsync<
                Dictionary<string, string>,
                SalePriceListGoodPrice[]>(
                HttpMethod.Get,
                "salepricelistgoodprices",
                priceRequest,
                ct);

            Console.WriteLine("RAW RESPONSE:");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                prices,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            Console.WriteLine();

            if (prices == null || prices.Length == 0)
            {
                Console.WriteLine("INFO: No prices found for this good");
                return;
            }

            _logger.LogInformation(
                "Found {Count} price entries for good {GoodId}",
                prices.Length,
                goodId);

            Console.WriteLine($"SUCCESS: Found {prices.Length} price entries");
            foreach (var price in prices)
            {
                Console.WriteLine($"  Price ID: {price.Id}");
                Console.WriteLine($"    Price: {price.Price}");
                Console.WriteLine($"    Price Type ID: {price.PriceTypeId}");
                Console.WriteLine($"    Updated: {price.Updated}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get good prices");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
