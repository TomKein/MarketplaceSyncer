using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using LinqToDB.Async;
using System.Text.Json;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;

namespace MarketplaceSyncer.Service;

using Microsoft.Extensions.Hosting;

// ...

public class ExperimentWorker : BackgroundService
{
    private readonly IBusinessRuClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExperimentWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ExperimentWorker(
        IBusinessRuClient client, 
        IServiceScopeFactory scopeFactory,
        ILogger<ExperimentWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Экспериментальный воркер запущен.");

        try
        {
            // Здесь можно проводить эксперименты
            var count = await _client.CountGoodsAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Всего товаров в Business.ru: {Count}", count);
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDataConnection>();

                // 1. Sync Groups
                _logger.LogInformation("Запрашиваю группы из Business.ru...");
                var groups = await _client.GetGroupsAsync(stoppingToken);
                _logger.LogInformation("Получено {Count} групп.", groups.Length);
                
                if (groups.Length > 0)
                {
                    // _logger.LogInformation("Пример группы: {@Group}", groups[0]); // Слишком много шума
                    
                    // Topological Sort Strategy
                    var existingIds = await db.Groups.Select(x => x.Id).ToListAsync(stoppingToken); // Load existing IDs
                    var knownIds = new HashSet<int>(existingIds);
                    
                    var pending = groups.ToList();
                    var processedCount = 0;
                    
                    _logger.LogInformation("Начинаю топологическую сортировку и сохранение. Всего: {Total}, Известных: {Known}", pending.Count, knownIds.Count);

                    while (pending.Count > 0)
                    {
                        var readyToProcess = new List<GroupResponse>();
                        var stillPending = new List<GroupResponse>();

                        foreach (var g in pending)
                        {
                            bool parentKnown = !g.ParentId.HasValue || knownIds.Contains(g.ParentId.Value);
                            if (parentKnown)
                            {
                                readyToProcess.Add(g);
                            }
                            else
                            {
                                stillPending.Add(g);
                            }
                        }

                        if (readyToProcess.Count == 0 && stillPending.Count > 0)
                        {
                            _logger.LogWarning("Обнаружено {Count} групп с неизвестными родителями (сироты или циклы). Сохраняю без ParentId.", stillPending.Count);
                            
                            foreach (var orphan in stillPending)
                            {
                                await UpsertGroupAsync(db, orphan, null, stoppingToken);
                                knownIds.Add(orphan.Id);
                            }
                            break; 
                        }

                        // Save batch
                        foreach (var g in readyToProcess)
                        {
                            await UpsertGroupAsync(db, g, g.ParentId, stoppingToken);
                            knownIds.Add(g.Id);
                            processedCount++;
                        }
                        
                        pending = stillPending;
                        // Логируем только если что-то сдвинулось
                        // _logger.LogInformation("Итерация завершена. Обработано: {Processed}, Осталось: {Pending}", processedCount, pending.Count);
                    }

                    _logger.LogInformation("Группы сохранены. Всего обработано: {Count}", processedCount);
                }

                // 2. Sync Units
                _logger.LogInformation("Запрашиваю единицы измерения...");
                var units = await _client.GetUnitsAsync(stoppingToken);
                _logger.LogInformation("Получено {Count} единиц.", units.Length);

                if (units.Length > 0)
                {
                    // _logger.LogInformation("Пример единицы: {@Unit}", units[0]);
                    
                    foreach (var u in units)
                    {
                        await db.Units.Merge()
                            .Using(new[] 
                            { 
                                new Unit 
                                { 
                                    Id = u.Id, 
                                    Name = u.Name, 
                                    FullName = u.FullName, 
                                    Code = u.Code,
                                    LastSyncedAt = DateTime.UtcNow 
                                } 
                            })
                            .OnTargetKey()
                            .UpdateWhenMatched()
                            .InsertWhenNotMatched()
                            .MergeAsync(stoppingToken); 
                    }
                     _logger.LogInformation("Единицы сохранены.");
                }

                var dbGroupsCount = await db.Groups.CountAsync(stoppingToken);
                var dbUnitsCount = await db.Units.CountAsync(stoppingToken);
                _logger.LogInformation("Итого в БД: Групп={G}, Единиц={U}", dbGroupsCount, dbUnitsCount);
            }

            // await Task.CompletedTask; // Не нужно
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка эксперимента");
        }
        finally
        {
             _logger.LogInformation("Экспериментальный воркер завершил работу. Остановка приложения...");
             _lifetime.StopApplication();
        }
    }

    private async Task UpsertGroupAsync(AppDataConnection db, GroupResponse g, int? parentId, CancellationToken token)
    {
        var exists = await db.Groups.AnyAsync(x => x.Id == g.Id, token);
        if (exists)
        {
            await db.Groups
                .Where(x => x.Id == g.Id)
                .Set(x => x.Name, g.Name)
                .Set(x => x.ParentId, parentId)
                .Set(x => x.LastSyncedAt, DateTime.UtcNow)
                // .Set(x => x.RawData, JsonSerializer.Serialize(g)) 
                .UpdateAsync(token);
        }
        else
        {
            await db.InsertAsync(new Group
            {
                Id = g.Id,
                Name = g.Name,
                ParentId = parentId,
                LastSyncedAt = DateTime.UtcNow,
                RawData = JsonSerializer.Serialize(g)
            }, token: token);
        }
    }
}
