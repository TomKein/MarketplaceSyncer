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
            // Эксперимент: Запрос типов цен продажи
            _logger.LogInformation("Запрашиваю типы цен продажи...");
            var priceTypes = await _client.GetPriceTypesAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Получено типов цен: {Count}", priceTypes.Length);
            
            foreach (var pt in priceTypes)
            {
                _logger.LogInformation("  Тип цены: Id={Id}, Name={Name}", pt.Id, pt.Name);
            }

            /* --- Остальные эксперименты временно отключены --- */
            
            // --- Verification for Attributes API ---
            _logger.LogInformation("Experiment: Fetching Attributes (attributesforgoods)...");
            var attributes = await _client.GetAttributesAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Fetched {Count} attributes.", attributes.Length);
            if (attributes.Length > 0)
            {
                _logger.LogInformation("Sample Attribute: {Json}", JsonSerializer.Serialize(attributes[0], new JsonSerializerOptions { WriteIndented = true }));
            }

            _logger.LogInformation("Experiment: Fetching Attribute Values (attributesforgoodsvalues)...");
            // Fetch values for the first attribute if available, or all/some defaults? 
            // The API allows fetching all without ID according to docs pattern seen, or we can try with null.
            var attributeValues = await _client.GetAttributeValuesAsync(cancellationToken: stoppingToken); 
            _logger.LogInformation("Fetched {Count} attribute values (total/sample).", attributeValues.Length);
            if (attributeValues.Length > 0)
            {
                 _logger.LogInformation("Sample Attribute Value: {Json}", JsonSerializer.Serialize(attributeValues[0], new JsonSerializerOptions { WriteIndented = true }));
            }

            _logger.LogInformation("Experiment: Fetching Good Attributes (goodsattributes)...");
             // Fetch good attributes (links) - trying without good_id to get a sample
            var goodAttributes = await _client.GetGoodAttributesAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Fetched {Count} good-attribute links.", goodAttributes.Length);
             if (goodAttributes.Length > 0)
            {
                 _logger.LogInformation("Sample Good Attribute Link: {Json}", JsonSerializer.Serialize(goodAttributes[0], new JsonSerializerOptions { WriteIndented = true }));
            }

            // --- Experiment: Stores and StoreGoods ---
            _logger.LogInformation("Experiment: Fetching Stores (stores)...");
            var stores = await _client.GetStoresAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Fetched {Count} stores.", stores.Length);
            foreach (var store in stores)
            {
                _logger.LogInformation("Store: Id={Id}, Name={Name}, RespEmp={EmpId}", store.Id, store.Name, store.ResponsibleEmployeeId);
            }

            if (stores.Length > 0)
            {
                _logger.LogInformation("Experiment: Fetching Store Goods (storegoods) for first store...");
                var storeGoods = await _client.GetStoreGoodsAsync(storeId: stores[0].Id, cancellationToken: stoppingToken);
                _logger.LogInformation("Fetched {Count} goods in store {StoreName}.", storeGoods.Length, stores[0].Name);

                if (storeGoods.Length > 0)
                {
                    _logger.LogInformation("Sample Store Good: {Json}", JsonSerializer.Serialize(storeGoods[0], new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            else 
            {
                 _logger.LogInformation("No stores found, skipping Store Goods experiment.");
            }

            /* 
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
            --- Конец отключенных экспериментов --- */
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

    private async Task UpsertGroupAsync(AppDataConnection db, GroupResponse g, long? parentId, CancellationToken token)
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
