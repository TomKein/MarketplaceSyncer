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
    private readonly ImageSyncService _images;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<GoodsSyncer> _logger;

    public GoodsSyncer(
        IBusinessRuClient client,
        AppDataConnection db,
        SyncStateRepository state,
        ImageSyncService images,
        IOptions<SynchronizationOptions> options,
        ILogger<GoodsSyncer> logger)
    {
        _client = client;
        _db = db;
        _state = state;
        _images = images;
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

        // --- Process Store Remains (Inline) ---
        if (apiGood.Remains is { Length: > 0 })
        {
            var existingRemains = await _db.StoreGoods.Where(sg => sg.GoodId == id).ToListAsync(ct);
            var existingRemainsMap = existingRemains.ToDictionary(sg => sg.StoreId);

            // UpdatedRemainsPrices stores timestamp for both prices and remains
            var remainsUpdatedAt = apiGood.UpdatedRemainsPrices;

            foreach (var remain in apiGood.Remains)
            {
                if (remain.Store == null || remain.Amount == null) continue;
                
                var storeId = remain.Store.Id;
                
                if (existingRemainsMap.TryGetValue(storeId, out _))
                {
                    // Update
                    await _db.StoreGoods
                        .Where(sg => sg.GoodId == id && sg.StoreId == storeId)
                        .Set(sg => sg.Amount, remain.Amount.Total)
                        .Set(sg => sg.Reserved, remain.Amount.Reserved)
                        .Set(sg => sg.RemainsMin, remain.Amount.RemainsMin ?? 0)
                        .Set(sg => sg.BusinessRuUpdatedAt, remainsUpdatedAt)
                        .Set(sg => sg.LastSyncedAt, DateTimeOffset.UtcNow)
                        .UpdateAsync(ct);

                    existingRemainsMap.Remove(storeId);
                }
                else
                {
                    // Insert
                    // Note: We assume Store exists (synced via ReferenceSyncer). 
                    // If not, FK violation might occur if not careful, but usually Stores are static.
                    await _db.InsertAsync(new StoreGood
                    {
                        GoodId = id,
                        StoreId = storeId,
                        Amount = remain.Amount.Total,
                        Reserved = remain.Amount.Reserved,
                        RemainsMin = remain.Amount.RemainsMin ?? 0,
                        BusinessRuUpdatedAt = remainsUpdatedAt,
                        LastSyncedAt = DateTimeOffset.UtcNow
                    }, token: ct);
                }
            }

            // Delete leftovers (if stock record removed from API?)
            // Usually stock 0 is sent, but if store link is removed completely:
            if (existingRemainsMap.Count > 0)
            {
                foreach (var key in existingRemainsMap.Keys)
                {
                    await _db.StoreGoods
                        .Where(sg => sg.GoodId == id && sg.StoreId == key)
                        .DeleteAsync(ct);
                }
            }
        }

        // --- Process Images (Inline) ---
        await SyncImagesInlineAsync(id, apiGood.Images, ct);
    }

    /// <summary>
    /// Синхронизация изображений inline из модели товара.
    /// Скачивает новые изображения и удаляет отсутствующие.
    /// </summary>
    private async Task SyncImagesInlineAsync(long goodId, MarketplaceSyncer.Service.BusinessRu.Models.Responses.GoodImageResponse[]? apiImages, CancellationToken ct)
    {
        // Получаем существующие изображения
        var existingImages = await _db.GoodImages
            .Where(i => i.GoodId == goodId)
            .ToListAsync(ct);

        if (apiImages is not { Length: > 0 })
        {
            // Удаляем все изображения, если в API их нет
            if (existingImages.Count > 0)
            {
                await _db.GoodImages.Where(i => i.GoodId == goodId).DeleteAsync(ct);
                _logger.LogDebug("Удалено {Count} изображений для товара {GoodId} (отсутствуют в API)", existingImages.Count, goodId);
            }
            return;
        }

        var existingByUrl = existingImages.ToDictionary(i => i.Url);
        var apiUrls = new HashSet<string>();

        foreach (var apiImage in apiImages)
        {
            if (string.IsNullOrEmpty(apiImage.Url))
                continue;

            apiUrls.Add(apiImage.Url);

            // Проверяем, есть ли уже такое изображение
            if (existingByUrl.TryGetValue(apiImage.Url, out var existing))
            {
                // Изображения иммутабельны - только обновляем sort если изменился
                if (existing.Sort != (apiImage.Sort ?? 0))
                {
                    await _db.GoodImages
                        .Where(i => i.Id == existing.Id)
                        .Set(i => i.Sort, apiImage.Sort ?? 0)
                        .UpdateAsync(ct);
                }
            }
            else
            {
                // Новое изображение - скачиваем
                try
                {
                    await _images.DownloadAndSaveImageAsync(goodId, apiImage, null, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка загрузки изображения {Url} для товара {GoodId}", apiImage.Url, goodId);
                }
            }
        }

        // Удаляем изображения, которых больше нет в API
        var toDelete = existingImages.Where(e => !apiUrls.Contains(e.Url)).ToList();
        foreach (var img in toDelete)
        {
            await _db.GoodImages.Where(i => i.Id == img.Id).DeleteAsync(ct);
            _logger.LogDebug("Удалено изображение {Id} для товара {GoodId} (отсутствует в API)", img.Id, goodId);
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
