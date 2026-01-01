namespace WorkerService1.TestMode;

public class TestModeWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestModeWorker> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public TestModeWorker(
        IServiceProvider serviceProvider,
        ILogger<TestModeWorker> logger,
        IHostApplicationLifetime appLifetime)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting in TEST MODE");
            
            using var scope = _serviceProvider.CreateScope();
            var batchTester = scope.ServiceProvider
                .GetRequiredService<BatchPriceUpdateTester>();
            
            await batchTester.RunBatchTestAsync(stoppingToken);
            
            _logger.LogInformation("Test mode completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test mode");
        }
        finally
        {
            _appLifetime.StopApplication();
        }
    }
}
