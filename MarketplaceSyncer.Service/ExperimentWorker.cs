using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using LinqToDB.Async;
using System.Text.Json;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.BusinessRu.Models.Requests;

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
            // --- Experiment: Stock Clearing (Warehouse 76726) ---
            _logger.LogInformation("Experiment: Stock Clearing for Warehouse 76726...");
            long targetStoreId = 76726;

            // 1. Get Store Goods (with server-side filtering)
            _logger.LogInformation("Fetching goods for store {StoreId} (only positive amount)...", targetStoreId);
            var storeGoods = await _client.GetStoreGoodsAsync(storeId: targetStoreId, withPositiveAmount: true, cancellationToken: stoppingToken);
            
            var positiveGoods = storeGoods.ToList();
            _logger.LogInformation("Found {Count} goods with positive amount.", positiveGoods.Count);

            if (positiveGoods.Count > 0)
            {
                // 2. Use Existing Charge Document
                long targetChargeId = 4409733; 
                _logger.LogInformation("Using existing Charge Document ID={Id}...", targetChargeId);

                // 3. Add Goods to Charge (One by One)
                _logger.LogInformation("Adding goods to Charge document...");
                int successCount = 0;
                
                foreach (var sg in positiveGoods)
                {
                    var goodReq = new ChargeGoodRequest
                    {
                        ChargeId = targetChargeId,
                        GoodId = sg.GoodId,
                        Amount = sg.Amount,
                        ModificationId = sg.ModificationId
                    };

                    try
                    {
                        await _client.AddChargeGoodAsync(goodReq, stoppingToken);
                        successCount++;
                        if (successCount % 10 == 0) _logger.LogInformation("Added {Count}/{Total} goods...", successCount, positiveGoods.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add good {GoodId} to charge.", sg.GoodId);
                    }
                }
                _logger.LogInformation("Finished adding goods. Successfully added: {Count}/{Total}", successCount, positiveGoods.Count);
            }
            else
            {
                 _logger.LogInformation("No goods with positive stock found. Nothing to clear.");
            }

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
        var id = g.Id;
        var existing = await db.Groups.FirstOrDefaultAsync(x => x.Id == id, token);
        var rawData = JsonSerializer.Serialize(g);

        if (existing != null)
        {
            await db.Groups
                .Where(x => x.Id == id)
                .Set(x => x.Name, g.Name)
                .Set(x => x.ParentId, parentId)
                .Set(x => x.Description, g.Description)
                .Set(x => x.DefaultOrder, g.DefaultOrder)
                .Set(x => x.IsDeleted, g.IsDeleted)
                .Set(x => x.BusinessRuUpdatedAt, g.Updated)
                .Set(x => x.RawData, rawData)
                .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                .UpdateAsync(token);
        }
        else
        {
            await db.InsertAsync(new Group
            {
                Id = id,
                Name = g.Name,
                ParentId = parentId,
                Description = g.Description,
                DefaultOrder = g.DefaultOrder,
                IsDeleted = g.IsDeleted,
                BusinessRuUpdatedAt = g.Updated,
                RawData = rawData,
                LastSyncedAt = DateTimeOffset.UtcNow
            }, token: token);
        }
    }
}
