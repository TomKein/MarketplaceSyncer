using MarketplaceSyncer.Service.BusinessRu.Client;

namespace MarketplaceSyncer.Service;

public class ExperimentWorker : BackgroundService
{
    private readonly IBusinessRuClient _client;
    private readonly ILogger<ExperimentWorker> _logger;

    public ExperimentWorker(IBusinessRuClient client, ILogger<ExperimentWorker> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Экспериментальный воркер запущен.");

        try
        {
            // Здесь можно проводить эксперименты
            var count = await _client.CountGoodsAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Всего товаров в Business.ru: {Count}", count);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка эксперимента");
        }
        
        _logger.LogInformation("Экспериментальный воркер завершил работу.");
    }
}
