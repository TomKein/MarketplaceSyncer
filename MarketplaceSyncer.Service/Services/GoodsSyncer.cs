using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Configuration;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using Microsoft.Extensions.Options;
using ApiGood = MarketplaceSyncer.Service.BusinessRu.Models.Responses.GoodResponse;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор товаров с поддержкой пагинации и дозагрузки
/// </summary>
public class GoodsSyncer
{
    private readonly IBusinessRuClient _client;
    private readonly AppDataConnection _db;
    private readonly SyncStateRepository _state;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<GoodsSyncer> _logger;

    public GoodsSyncer(
        IBusinessRuClient client,
        AppDataConnection db,
        SyncStateRepository state,
        IOptions<SynchronizationOptions> options,
        ILogger<GoodsSyncer> logger)
    {
        _client = client;
        _db = db;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Инкрементальная синхронизация (MEDIUM приоритет)
    /// </summary>
    public async Task RunDeltaSyncAsync(CancellationToken ct = default)
    {
        var lastDelta = await _state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        var since = lastDelta ?? DateTimeOffset.UtcNow.AddDays(-1);

        _logger.LogInformation("Delta sync товаров с {Since}...", since);

        var changedGoods = await _client.GetGoodsChangedAfterAsync(since, cancellationToken: ct);
        _logger.LogInformation("Получено {Count} изменённых товаров", changedGoods.Length);

        foreach (var apiGood in changedGoods)
        {
            await UpsertGoodAsync(apiGood, ct);
        }

        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTimeOffset.UtcNow, ct);
        _logger.LogInformation("Delta sync завершён: {Count} товаров обработано", changedGoods.Length);
    }

    /// <summary>
    /// Инициальная загрузка с контролем страниц (HIGH приоритет)
    /// </summary>
    public async Task RunInitialSyncAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем инициальную загрузку товаров...");

        // Получаем общее количество для расчёта страниц
        var totalCount = await _client.CountGoodsAsync(cancellationToken: ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
        
        await _state.SetIntAsync(SyncStateKeys.InitialGoodsTotalPages, totalPages, ct);
        
        var currentPage = await _state.GetIntAsync(SyncStateKeys.InitialGoodsPage, 1, ct);
        _logger.LogInformation("Инициальная загрузка: страница {Current}/{Total}", currentPage, totalPages);

        while (currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var goods = await LoadGoodsPageAsync(currentPage, ct);
            _logger.LogInformation("Страница {Page}/{Total}: загружено {Count} товаров", 
                currentPage, totalPages, goods.Length);

            foreach (var apiGood in goods)
            {
                await UpsertGoodAsync(apiGood, ct);
            }

            currentPage++;
            await _state.SetIntAsync(SyncStateKeys.InitialGoodsPage, currentPage, ct);
        }

        // Сбрасываем прогресс
        await _state.SetIntAsync(SyncStateKeys.InitialGoodsPage, 1, ct);
        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTimeOffset.UtcNow, ct);
        
        _logger.LogInformation("Инициальная загрузка товаров завершена: {Total} страниц", totalPages);
    }

    /// <summary>
    /// Загрузка одного чанка для full reload (LOW приоритет)
    /// </summary>
    public async Task<bool> RunFullReloadChunkAsync(CancellationToken ct = default)
    {
        var currentPage = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, 1, ct);
        var totalPages = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, 0, ct);

        // Если totalPages = 0, нужно инициализировать
        if (totalPages == 0)
        {
            var totalCount = await _client.CountGoodsAsync(cancellationToken: ct);
            totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
            await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, totalPages, ct);
            await _state.SetDateTimeOffsetAsync(SyncStateKeys.FullReloadGoodsStartedAt, DateTimeOffset.UtcNow, ct);
            _logger.LogInformation("Full reload инициализирован: {Total} страниц", totalPages);
        }

        // Загружаем ChunkSize страниц
        var pagesLoaded = 0;
        while (pagesLoaded < _options.FullReloadChunkSize && currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var goods = await LoadGoodsPageAsync(currentPage, ct);
            foreach (var apiGood in goods)
            {
                await UpsertGoodAsync(apiGood, ct);
            }

            _logger.LogDebug("Full reload: страница {Page}/{Total}", currentPage, totalPages);
            
            currentPage++;
            pagesLoaded++;
            await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, currentPage, ct);
        }

        // Проверяем завершение цикла
        if (currentPage <= totalPages) return true; // Есть ещё работа
        await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, 1, ct);
        await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, 0, ct);
        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastFull, DateTimeOffset.UtcNow, ct);
        _logger.LogInformation("Full reload завершён!");
        return false; // Больше нет работы

    }

    /// <summary>
    /// Проверить, есть ли незавершённая работа full reload
    /// </summary>
    public async Task<bool> HasPendingFullReloadWorkAsync(CancellationToken ct = default)
    {
        // Проверяем, нужен ли full reload по времени
        // Проверяем, нужен ли full reload по времени
        var lastFull = await _state.GetLastRunAsync(SyncStateKeys.GoodsLastFull, ct);
        if (lastFull != null && DateTimeOffset.UtcNow - lastFull.Value < _options.FullReloadTargetInterval)
        {
            // Full reload ещё не нужен
            var currentPage = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, 1, ct);
            var totalPages = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, 0, ct);
            // Но если есть незавершённый — продолжаем
            return totalPages > 0 && currentPage <= totalPages;
        }
        return true; // Нужен full reload
    }

    private async Task<ApiGood[]> LoadGoodsPageAsync(int page, CancellationToken ct)
    {
        // Используем основной метод с пагинацией
        // TODO: добавить поддержку пагинации в API клиент
        var allGoods = await _client.GetGoodsAsync(cancellationToken: ct);
        var skip = (page - 1) * _options.PageSize;
        return allGoods.Skip(skip).Take(_options.PageSize).ToArray();
    }

    private async Task UpsertGoodAsync(ApiGood apiGood, CancellationToken ct)
    {
        var id = apiGood.Id;
        var existing = await _db.Goods.FirstOrDefaultAsync(g => g.Id == id, ct);

        var rawData = JsonSerializer.Serialize(apiGood);
        var price = apiGood.Prices?.FirstOrDefault()?.Price ?? 0;

        if (existing != null)
        {
            await _db.Goods
                .Where(g => g.Id == id)
                .Set(g => g.Name, apiGood.Name ?? "")
                .Set(g => g.Article, apiGood.PartNumber)
                .Set(g => g.Code, apiGood.StoreCode)
                .Set(g => g.IsArchive, apiGood.Archive)
                .Set(g => g.Price, price)
                .Set(g => g.RawData, rawData)
                .Set(g => g.LastSyncedAt, DateTimeOffset.UtcNow)
                .Set(g => g.InternalUpdatedAt, DateTimeOffset.UtcNow)
                .UpdateAsync(ct);
        }
        else
        {
            await _db.InsertAsync(new Good
            {
                Id = id,
                Name = apiGood.Name ?? "",
                Article = apiGood.PartNumber,
                Code = apiGood.StoreCode,
                IsArchive = apiGood.Archive,
                Price = price,
                RawData = rawData,
                LastSyncedAt = DateTimeOffset.UtcNow,
                InternalUpdatedAt = DateTimeOffset.UtcNow
            }, token: ct);
        }

        // --- Process Attributes (Inline) ---
        if (apiGood.Attributes is { Length: > 0 })
        {
            var existingLinks = await _db.GoodAttributes.Where(ga => ga.GoodId == id).ToListAsync(ct);
            var existingMap = existingLinks.ToDictionary(gl => gl.Id); // ID from API
            
            foreach (var attr in apiGood.Attributes)
            {
                if (existingMap.TryGetValue(attr.Id, out _))
                {
                    // Update
                    await _db.GoodAttributes
                        .Where(gl => gl.Id == attr.Id)
                        .Set(gl => gl.AttributeId, attr.AttributeId)
                        .Set(gl => gl.ValueId, attr.ValueId)
                        .Set(gl => gl.Value, attr.Value)
                        .Set(gl => gl.BusinessRuUpdatedAt, attr.Updated)
                        .Set(gl => gl.LastSyncedAt, DateTimeOffset.UtcNow)
                        .UpdateAsync(ct);
                        
                    existingMap.Remove(attr.Id);
                }
                else
                {
                    // Insert
                    try 
                    {
                        await _db.InsertAsync(new GoodAttribute
                        {
                            Id = attr.Id,
                            GoodId = id,
                            AttributeId = attr.AttributeId,
                            ValueId = attr.ValueId,
                            Value = attr.Value,
                            BusinessRuUpdatedAt = attr.Updated,
                            LastSyncedAt = DateTimeOffset.UtcNow
                        }, token: ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to insert GoodAttribute {Id} for Good {GoodId}", attr.Id, id);
                    }
                }
            }
            
            // Delete leftovers
            if (existingMap.Count > 0)
            {
                 foreach (var key in existingMap.Keys)
                 {
                     await _db.GoodAttributes.Where(gl => gl.Id == key).DeleteAsync(ct);
                 }
        }
        }

        // --- Process Prices ---
        // --- Process Prices ---
        if (apiGood.Prices is { Length: > 0 })
        {
            var existingPrices = await _db.GoodPrices.Where(gp => gp.GoodId == id).ToListAsync(ct);
            var existingPricesMap = existingPrices.ToDictionary(p => p.PriceTypeId);

            // UpdatedRemainsPrices is now DateTimeOffset? handled by JsonConverter
            var priceUpdatedAt = apiGood.UpdatedRemainsPrices;

            foreach (var goodPrice in apiGood.Prices)
            {
                // Logic to get TypeId: Try nested object first, then flat property
                long? typeId = null;
                if (goodPrice.PriceType != null)
                {
                    typeId = goodPrice.PriceType.Id;
                }
                else if (long.TryParse(goodPrice.TypeId, out var tid))
                {
                    typeId = tid;
                }

                if (typeId.HasValue && goodPrice.Price.HasValue)
                {
                    var currency = goodPrice.Currency ?? goodPrice.PriceType?.Currency?.ShortName; // fallback currency

                    if (existingPricesMap.TryGetValue(typeId.Value, out _))
                    {
                        // Update
                        await _db.GoodPrices
                            .Where(gp => gp.GoodId == id && gp.PriceTypeId == typeId.Value)
                            .Set(gp => gp.Price, goodPrice.Price.Value)
                            .Set(gp => gp.Currency, currency)
                            .Set(gp => gp.BusinessRuUpdatedAt, priceUpdatedAt)
                            .Set(gp => gp.LastSyncedAt, DateTimeOffset.UtcNow)
                            .UpdateAsync(ct);
                            
                         existingPricesMap.Remove(typeId.Value);
                    }
                    else
                    {
                        // Insert
                        await _db.InsertAsync(new GoodPrice
                        {
                            GoodId = id,
                            PriceTypeId = typeId.Value,
                            Price = goodPrice.Price.Value,
                            Currency = currency,
                            BusinessRuUpdatedAt = priceUpdatedAt,
                            LastSyncedAt = DateTimeOffset.UtcNow
                        }, token: ct);
                    }
                }
            }
            
            // Delete leftovers
            if (existingPricesMap.Count > 0)
            {
                foreach (var key in existingPricesMap.Keys)
                {
                    await _db.GoodPrices
                        .Where(gp => gp.GoodId == id && gp.PriceTypeId == key)
                        .DeleteAsync(ct);
                }
            }
        }
    }

    // ========== Public helpers for InitialSyncRunner ==========

    /// <summary>
    /// Получить общее количество товаров
    /// </summary>
    public Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => _client.CountGoodsAsync(cancellationToken: ct);

    /// <summary>
    /// Загрузить и сохранить одну страницу товаров
    /// </summary>
    public async Task LoadAndSavePageAsync(int page, CancellationToken ct = default)
    {
        var goods = await LoadGoodsPageAsync(page, ct);
        foreach (var apiGood in goods)
        {
            await UpsertGoodAsync(apiGood, ct);
        }
    }

    /// <summary>
    /// Получить список ID всех товаров из БД
    /// </summary>
    public async Task<List<long>> GetAllGoodIdsAsync(CancellationToken ct = default)
    {
        return await _db.Goods
            .Select(g => g.Id)
            .ToListAsync(ct);
    }
}
