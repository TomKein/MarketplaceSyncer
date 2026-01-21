using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор текущих цен (endpoint currentprices).
/// </summary>
public class PriceSyncer(
    IBusinessRuClient client,
    AppDataConnection db,
    SyncStateRepository state,
    ILogger<PriceSyncer> logger)
{
    /// <summary>
    /// Инкрементальная синхронизация цен.
    /// </summary>
    public async Task RunDeltaSyncAsync(CancellationToken ct = default)
    {
        var lastDelta = await state.GetLastRunAsync(SyncStateKeys.PricesLastDelta, ct);
        var since = lastDelta ?? DateTimeOffset.UtcNow.AddDays(-7);

        logger.LogInformation("Delta sync цен с {Since}...", since);

        var changedPrices = await client.GetCurrentPricesAsync(changedAfter: since, cancellationToken: ct);
        logger.LogInformation("Получено {Count} изменённых цен", changedPrices.Length);

        if (changedPrices.Length == 0)
        {
            await state.SetLastRunAsync(SyncStateKeys.PricesLastDelta, DateTimeOffset.UtcNow, ct);
            return;
        }

        // Группируем по GoodId + PriceTypeId для upsert
        foreach (var price in changedPrices)
        {
            await UpsertPriceAsync(price, ct);
        }

        await state.SetLastRunAsync(SyncStateKeys.PricesLastDelta, DateTimeOffset.UtcNow, ct);
        logger.LogInformation("Delta sync цен завершён: {Count} записей обработано", changedPrices.Length);
    }

    private async Task UpsertPriceAsync(BusinessRu.Models.Responses.CurrentPriceResponse price, CancellationToken ct)
    {
        // Проверяем, существует ли товар (FK constraint)
        var goodExists = await db.Goods.AnyAsync(g => g.Id == price.GoodId, ct);
        if (!goodExists)
        {
            logger.LogDebug("Пропуск цены для товара {GoodId} - товар не найден в БД", price.GoodId);
            return;
        }

        var existing = await db.GoodPrices
            .FirstOrDefaultAsync(gp => gp.GoodId == price.GoodId && gp.PriceTypeId == price.PriceTypeId, ct);

        if (existing != null)
        {
            await db.GoodPrices
                .Where(gp => gp.GoodId == price.GoodId && gp.PriceTypeId == price.PriceTypeId)
                .Set(gp => gp.Price, price.Price)
                .Set(gp => gp.BusinessRuUpdatedAt, price.Updated)
                .Set(gp => gp.LastSyncedAt, DateTimeOffset.UtcNow)
                .UpdateAsync(ct);
        }
        else
        {
            await db.InsertAsync(new GoodPrice
            {
                GoodId = price.GoodId,
                PriceTypeId = price.PriceTypeId,
                Price = price.Price,
                BusinessRuUpdatedAt = price.Updated,
                LastSyncedAt = DateTimeOffset.UtcNow
            }, token: ct);
        }
    }
}
