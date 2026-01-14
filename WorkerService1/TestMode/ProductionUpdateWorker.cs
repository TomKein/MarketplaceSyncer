using LinqToDB;
using Microsoft.Extensions.Options;
using WorkerService1.BusinessRu.Client;
using WorkerService1.Configuration;
using WorkerService1.Data;

namespace WorkerService1.TestMode;

public class ProductionUpdateWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ProductionUpdateArgs _args;
    private readonly ILogger<ProductionUpdateWorker> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public ProductionUpdateWorker(
        IServiceProvider serviceProvider,
        ProductionUpdateArgs args,
        ILogger<ProductionUpdateWorker> logger,
        IHostApplicationLifetime appLifetime)
    {
        _serviceProvider = serviceProvider;
        _args = args;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // ============================================================
            // Delete it!!!
            // ONE-TIME SETUP: Create price list and clear database
            // ============================================================
            // using (var setupScope = _serviceProvider.CreateScope())
            // {
            //     var client = setupScope.ServiceProvider.GetRequiredService<IBusinessRuClient>();
            //     var db = setupScope.ServiceProvider.GetRequiredService<AppDbConnection>();
            //     var options = setupScope.ServiceProvider.GetRequiredService<IOptions<PriceUpdateOptions>>().Value;
            //
            //     Console.WriteLine();
            //     Console.WriteLine("=" + new string('=', 79));
            //     Console.WriteLine("ONE-TIME SETUP - Creating Price List and Clearing Database");
            //     Console.WriteLine("=" + new string('=', 79));
            //     Console.WriteLine();
            //
            //     // Создаем прайс-лист с датой 01.01.2026 00:00:01
            //     var priceListName = "Price Update 2026-01-01 00:00:01";
            //     
            //     Console.WriteLine($"Creating price list: {priceListName}");
            //     Console.WriteLine($"Price type ID: {options.TargetPriceTypeId}");
            //     
            //     var priceListId = await client.CreatePriceListAsync(
            //         priceListName,
            //         options.TargetPriceTypeId,
            //         stoppingToken);
            //
            //     Console.WriteLine();
            //     Console.WriteLine("✓ Price list created successfully!");
            //     Console.WriteLine($"  ID: {priceListId}");
            //     Console.WriteLine();
            //     Console.WriteLine("Copy this ID to Program.cs line 16:");
            //     Console.WriteLine($"  var priceListId = \"{priceListId}\";");
            //     Console.WriteLine();
            //
            //     // Очищаем таблицу good_prices
            //     Console.WriteLine("Clearing good_prices table...");
            //     var deletedCount = db.GoodPrices.Delete();
            //     Console.WriteLine($"✓ Deleted {deletedCount} records from good_prices");
            //     
            //     Console.WriteLine();
            //     Console.WriteLine("=" + new string('=', 79));
            //     Console.WriteLine("SETUP COMPLETED - Now comment out this code and restart!");
            //     Console.WriteLine("=" + new string('=', 79));
            //     Console.WriteLine();
            //     
            //     return; // Stop execution
            // }
            // ============================================================
            // End of one-time setup code
            // ============================================================
            
            if (string.IsNullOrWhiteSpace(_args.PriceListId))
            {
                _logger.LogError(
                    "Price list ID is required. " +
                    "Use --price-list-id=<ID>");
                Console.WriteLine();
                Console.WriteLine("ERROR: Price list ID is required");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine(
                    "  dotnet run -- --update-all-prices " +
                    "--price-list-id=4257744");
                Console.WriteLine();
                Console.WriteLine("Optional:");
                Console.WriteLine(
                    "  --start-from-page=<N>  Resume from specific page");
                Console.WriteLine();
                return;
            }
            
            _logger.LogInformation(
                "Starting production price update");

            using var scope = _serviceProvider.CreateScope();
            var tester = scope.ServiceProvider
                .GetRequiredService<ProductionUpdateTester>();

            await tester.RunProductionUpdateAsync(
                _args.PriceListId,
                _args.StartFromPage,
                stoppingToken);

            _logger.LogInformation(
                "Production price update completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Production update error");
        }
        finally
        {
            _appLifetime.StopApplication();
        }
    }
}
