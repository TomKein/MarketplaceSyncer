using WorkerService1.Configuration;

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
