using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using LinqToDB.Async;

namespace MarketplaceSyncer.Service;

public class ExperimentWorker : BackgroundService
{
    private readonly IBusinessRuClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExperimentWorker> _logger;

    public ExperimentWorker(
        IBusinessRuClient client, 
        IServiceScopeFactory scopeFactory,
        ILogger<ExperimentWorker> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
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
            
            /*
            // Тест БД
            _logger.LogInformation("Начинаю тест БД...");
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDataConnection>();

                // 1. Очистка (для идемпотентности теста)
                await db.Goods.DeleteAsync(stoppingToken);
                await db.Groups.DeleteAsync(stoppingToken);
                await db.Units.DeleteAsync(stoppingToken);

                // 2. Вставка
                var testGroup = new Group
                {
                    Id = 1001,
                    Name = "Test Group",
                    LastSyncedAt = DateTime.UtcNow
                };
                await db.InsertAsync(testGroup, token: stoppingToken);
                
                var testUnit = new Unit
                {
                    Id = 1,
                    Name = "шт",
                    LastSyncedAt = DateTime.UtcNow
                };
                await db.InsertAsync(testUnit, token: stoppingToken);

                var testGood = new Good
                {
                    Id = 5005,
                    Name = "Test Good",
                    GroupId = 1001,
                    UnitId = 1,
                    SyncStatus = 0,
                    InternalUpdatedAt = DateTime.UtcNow
                };
                await db.InsertAsync(testGood, token: stoppingToken);
                
                _logger.LogInformation("Данные вставлены.");

                // 3. Чтение
                var goodsCount = await db.Goods.CountAsync(stoppingToken);
                var loadedGood = await db.Goods
                    .LoadWith(g => g.Group)
                    .FirstOrDefaultAsync(g => g.Id == 5005, stoppingToken);

                _logger.LogInformation("Товаров в БД: {Count}", goodsCount);
                if (loadedGood != null)
                {
                    _logger.LogInformation("Загружен товар: {Name}, GroupId: {GroupId}", loadedGood.Name, loadedGood.GroupId);
                }
                else
                {
                    _logger.LogError("Товар не найден!");
                }
            }
            */

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка эксперимента");
        }
        
        _logger.LogInformation("Экспериментальный воркер завершил работу.");
    }
}
