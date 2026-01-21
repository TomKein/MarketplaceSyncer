using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор остатков на складах (endpoint storegoods).
/// </summary>
public class StockSyncer(
    IBusinessRuClient client,
    AppDataConnection db,
    SyncStateRepository state,
    ILogger<StockSyncer> logger)
{
    /// <summary>
    /// Инкрементальная синхронизация остатков.
    /// </summary>
    public async Task RunDeltaSyncAsync(CancellationToken ct = default)
    {
        var lastDelta = await state.GetLastRunAsync(SyncStateKeys.StockLastDelta, ct);
        var since = lastDelta ?? DateTimeOffset.UtcNow.AddDays(-7);

        logger.LogInformation("Delta sync остатков с {Since}...", since);

        var changedStocks = await client.GetStoreGoodsAsync(changedAfter: since, cancellationToken: ct);
        logger.LogInformation("Получено {Count} изменённых остатков", changedStocks.Length);

        if (changedStocks.Length == 0)
        {
            await state.SetLastRunAsync(SyncStateKeys.StockLastDelta, DateTimeOffset.UtcNow, ct);
            return;
        }

        foreach (var stock in changedStocks)
        {
            await UpsertStockAsync(stock, ct);
        }

        await state.SetLastRunAsync(SyncStateKeys.StockLastDelta, DateTimeOffset.UtcNow, ct);
        logger.LogInformation("Delta sync остатков завершён: {Count} записей обработано", changedStocks.Length);
    }

    private async Task UpsertStockAsync(BusinessRu.Models.Responses.StoreGoodResponse stock, CancellationToken ct)
    {
        // Проверяем FK constraints
        var goodExists = await db.Goods.AnyAsync(g => g.Id == stock.GoodId, ct);
        if (!goodExists)
        {
            logger.LogDebug("Пропуск остатка для товара {GoodId} - товар не найден в БД", stock.GoodId);
            return;
        }

        var storeExists = await db.Stores.AnyAsync(s => s.Id == stock.StoreId, ct);
        if (!storeExists)
        {
            logger.LogDebug("Пропуск остатка для склада {StoreId} - склад не найден в БД", stock.StoreId);
            return;
        }

        var existing = await db.StoreGoods
            .FirstOrDefaultAsync(sg => sg.Id == stock.Id, ct);

        if (existing != null)
        {
            await db.StoreGoods
                .Where(sg => sg.Id == stock.Id)
                .Set(sg => sg.StoreId, stock.StoreId)
                .Set(sg => sg.GoodId, stock.GoodId)
                .Set(sg => sg.ModificationId, stock.ModificationId)
                .Set(sg => sg.Amount, stock.Amount)
                .Set(sg => sg.Reserved, stock.Reserved)
                .Set(sg => sg.RemainsMin, stock.RemainsMin ?? 0)
                .Set(sg => sg.BusinessRuUpdatedAt, stock.Updated)
                .Set(sg => sg.LastSyncedAt, DateTimeOffset.UtcNow)
                .UpdateAsync(ct);
        }
        else
        {
            await db.InsertAsync(new StoreGood
            {
                Id = stock.Id,
                StoreId = stock.StoreId,
                GoodId = stock.GoodId,
                ModificationId = stock.ModificationId,
                Amount = stock.Amount,
                Reserved = stock.Reserved,
                RemainsMin = stock.RemainsMin ?? 0,
                BusinessRuUpdatedAt = stock.Updated,
                LastSyncedAt = DateTimeOffset.UtcNow
            }, token: ct);
        }
    }
}
