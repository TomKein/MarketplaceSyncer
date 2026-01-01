namespace WorkerService1.TestMode;

public class TestModeWorker : BackgroundService
{
    private readonly ApiTester _apiTester;
    private readonly ILogger<TestModeWorker> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public TestModeWorker(
        ApiTester apiTester,
        ILogger<TestModeWorker> logger,
        IHostApplicationLifetime appLifetime)
    {
        _apiTester = apiTester;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting in TEST MODE");
            
            await _apiTester.RunAllTestsAsync(stoppingToken);
            
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
