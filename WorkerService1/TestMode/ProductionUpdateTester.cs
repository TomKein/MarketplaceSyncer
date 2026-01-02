using WorkerService1.Services;

namespace WorkerService1.TestMode;

public class ProductionUpdateTester
{
    private readonly ProductionPriceUpdateRunner _runner;
    private readonly ILogger<ProductionUpdateTester> _logger;

    public ProductionUpdateTester(
        ProductionPriceUpdateRunner runner,
        ILogger<ProductionUpdateTester> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task RunProductionUpdateAsync(
        string priceListId,
        int? startFromPage,
        CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("PRODUCTION PRICE UPDATE - ALL GOODS");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"Price List ID: {priceListId}");
        Console.WriteLine($"Starting from page: {startFromPage ?? 1}");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop (progress will be saved)");
        Console.WriteLine();
        Console.WriteLine("Starting in 3 seconds...");
        
        await Task.Delay(3000, ct);

        try
        {
            var stats = await _runner.RunUpdateAsync(
                priceListId,
                startFromPage,
                ct);

            _logger.LogInformation(
                "Production update completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Production update cancelled by user");
            Console.WriteLine();
            Console.WriteLine("UPDATE CANCELLED - Progress saved to database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Production update failed");
            Console.WriteLine();
            Console.WriteLine($"UPDATE FAILED: {ex.Message}");
            throw;
        }
    }
}
