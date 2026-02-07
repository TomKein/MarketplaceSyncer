using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор справочников (страны, валюты, ед. измерения, группы) и отношений (товары-ед. измерения).
/// </summary>
public class ReferenceSyncer
{
    private readonly IBusinessRuClient _client;
    private readonly AppDataConnection _db;
    private readonly ILogger<ReferenceSyncer> _logger;

    public ReferenceSyncer(
        IBusinessRuClient client,
        AppDataConnection db,
        ILogger<ReferenceSyncer> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Полная синхронизация всех простых справочников (перед товарами).
    /// </summary>
    public async Task SyncDictionariesAsync(CancellationToken ct = default)
    {
        await SyncGroupsAsync(ct);
        await SyncCountriesAsync(ct);
        await SyncCurrenciesAsync(ct);
        await SyncMeasuresAsync(ct);
        await SyncPriceTypesAsync(ct);
        await SyncStoresAsync(ct);
    }
    

    /// <summary>
    /// Синхронизация отношений товаров и единиц измерения (после товаров).
    /// </summary>
    public async Task SyncGoodsRelationsAsync(CancellationToken ct = default)
    {
        await SyncGoodsMeasuresAsync(ct);
    }

    public async Task SyncGroupsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию групп...");
        
        var apiGroups = await _client.GetGroupsAsync(ct);
        _logger.LogInformation("Получено {Count} групп из API", apiGroups.Length);

        // Топологическая сортировка (BFS от корней к листьям)
        var lookup = apiGroups.ToLookup(g => g.ParentId);
        var sortedGroups = new List<GroupResponse>();
        var queue = new Queue<GroupResponse>();

        // 1. Корневые элементы (ParentId == null)
        foreach (var root in lookup[null])
        {
            queue.Enqueue(root);
        }

        // 2. Обход в ширину
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sortedGroups.Add(current);

            foreach (var child in lookup[current.Id])
            {
                queue.Enqueue(child);
            }
        }

        // 3. Сироты
        if (sortedGroups.Count < apiGroups.Length)
        {
            var processedIds = sortedGroups.Select(g => g.Id).ToHashSet();
            var orphans = apiGroups.Where(g => !processedIds.Contains(g.Id)).ToList();
            _logger.LogWarning("Обнаружено {Count} изолированных групп или циклов.", orphans.Count);
            sortedGroups.AddRange(orphans);
        }

        foreach (var apiGroup in sortedGroups)
        {
            var id = apiGroup.Id;
            var existing = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct);

            if (existing != null)
            {
                var rawData = JsonSerializer.Serialize(apiGroup);
                await _db.Groups
                    .Where(g => g.Id == id)
                    .Set(g => g.Name, apiGroup.Name)
                    .Set(g => g.ParentId, apiGroup.ParentId)
                    .Set(g => g.Description, apiGroup.Description)
                    .Set(g => g.DefaultOrder, apiGroup.DefaultOrder)
                    .Set(g => g.IsDeleted, apiGroup.IsDeleted)
                    .Set(g => g.BusinessRuUpdatedAt, apiGroup.Updated)
                    .Set(g => g.RawData, rawData)
                    .Set(g => g.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                var rawData = JsonSerializer.Serialize(apiGroup);
                await _db.InsertAsync(new Group
                {
                    Id = id,
                    Name = apiGroup.Name ?? "",
                    ParentId = apiGroup.ParentId,
                    Description = apiGroup.Description,
                    DefaultOrder = apiGroup.DefaultOrder,
                    IsDeleted = apiGroup.IsDeleted,
                    BusinessRuUpdatedAt = apiGroup.Updated,
                    RawData = rawData,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }
        _logger.LogInformation("Синхронизация групп завершена.");
    }

    public async Task SyncStoresAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию складов (stores)...");
        var items = await _client.GetStoresAsync(ct);
        _logger.LogInformation("Получено {Count} складов.", items.Length);

        foreach (var item in items)
        {
            var id = item.Id;
            var existing = await _db.Stores.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (existing != null)
            {
                await _db.Stores
                    .Where(x => x.Id == id)
                    .Set(x => x.Name, item.Name)
                    .Set(x => x.Address, item.Address)
                    .Set(x => x.IsArchive, item.Archive)
                    .Set(x => x.IsDeleted, item.Deleted)
                    .Set(x => x.DenyNegativeBalance, item.DenyNegativeBalance)
                    .Set(x => x.ResponsibleEmployeeId, item.ResponsibleEmployeeId)
                    .Set(x => x.DebitType, item.DebitType)
                    .Set(x => x.BusinessRuUpdatedAt, item.Updated)
                    .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Store
                {
                    Id = id,
                    Name = item.Name,
                    Address = item.Address,
                    IsArchive = item.Archive,
                    IsDeleted = item.Deleted,
                    DenyNegativeBalance = item.DenyNegativeBalance,
                    ResponsibleEmployeeId = item.ResponsibleEmployeeId,
                    DebitType = item.DebitType,
                    BusinessRuUpdatedAt = item.Updated,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }
        _logger.LogInformation("Синхронизация складов завершена.");
    }

    public async Task SyncCountriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию стран...");
        var items = await _client.GetCountriesAsync(ct);
        _logger.LogInformation("Получено {Count} стран.", items.Length);

        foreach (var item in items)
        {
            var id = item.Id;
            var existing = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (existing != null)
            {
                await _db.Countries
                    .Where(x => x.Id == id)
                    .Set(x => x.Name, item.Name)
                    .Set(x => x.FullName, item.FullName)
                    .Set(x => x.InternationalName, item.InternationalName)
                    .Set(x => x.Code, item.Code)
                    .Set(x => x.Alfa2, item.Alfa2)
                    .Set(x => x.Alfa3, item.Alfa3)
                    .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Country
                {
                    Id = id,
                    Name = item.Name,
                    FullName = item.FullName,
                    InternationalName = item.InternationalName,
                    Code = item.Code,
                    Alfa2 = item.Alfa2,
                    Alfa3 = item.Alfa3,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }
        _logger.LogInformation("Синхронизация стран завершена.");
    }

    public async Task SyncCurrenciesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию валют...");
        var items = await _client.GetCurrenciesAsync(ct);
        _logger.LogInformation("Получено {Count} валют.", items.Length);

        foreach (var item in items)
        {
            var id = item.Id;
            var existing = await _db.Currencies.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (existing != null)
            {
                await _db.Currencies
                    .Where(x => x.Id == id)
                    .Set(x => x.Name, item.Name)
                    .Set(x => x.ShortName, item.ShortName)
                    .Set(x => x.NameIso, item.NameIso)
                    .Set(x => x.CodeIso, item.CodeIso)
                    .Set(x => x.IsDefault, item.Default)
                    .Set(x => x.IsUser, item.User)
                    .Set(x => x.UserValue, item.UserValue)
                    .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Currency
                {
                    Id = id,
                    Name = item.Name,
                    ShortName = item.ShortName,
                    NameIso = item.NameIso,
                    CodeIso = item.CodeIso,
                    IsDefault = item.Default,
                    IsUser = item.User,
                    UserValue = item.UserValue,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }
        _logger.LogInformation("Синхронизация валют завершена.");
    }

    public async Task SyncMeasuresAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию единиц измерения (measures)...");
        var items = await _client.GetMeasuresAsync(ct);
        _logger.LogInformation("Получено {Count} единиц измерения.", items.Length);

        foreach (var item in items)
        {
            var id = item.Id;
            var existing = await _db.Measures.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (existing != null)
            {
                await _db.Measures
                    .Where(x => x.Id == id)
                    .Set(x => x.Name, item.Name)
                    .Set(x => x.ShortName, item.ShortName)
                    .Set(x => x.Okei, item.Okei)
                    .Set(x => x.IsDefault, item.Default)
                    .Set(x => x.IsArchive, item.Archive)
                    .Set(x => x.IsDeleted, item.Deleted)
                    .Set(x => x.BusinessRuUpdatedAt, item.Updated)
                    .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Measure
                {
                    Id = id,
                    Name = item.Name,
                    ShortName = item.ShortName,
                    Okei = item.Okei,
                    IsDefault = item.Default,
                    IsArchive = item.Archive,
                    IsDeleted = item.Deleted,
                    BusinessRuUpdatedAt = item.Updated,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }
        _logger.LogInformation("Синхронизация единиц измерения завершена.");
    }

    public async Task SyncGoodsMeasuresAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию отношений товаров и единиц измерения...");
        // This likely returns ALL if no filter. Or we can filter by update.
        // For now, full sync or basic iteration.
        var items = await _client.GetGoodsMeasuresAsync(cancellationToken: ct);
        _logger.LogInformation("Получено {Count} отношений из API.", items.Length);

        // Batch processing or simple loop? Simple loop is ok for starters.
        // Need to be careful about FKs. If Good or Measure doesn't exist, we skip or log?
        // Usually we expect Goods and Measures to be synced.
        
        foreach (var item in items)
        {
            var id = item.Id;
            var existing = await _db.GoodsMeasures.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (existing != null)
            {
                await _db.GoodsMeasures
                    .Where(x => x.Id == id)
                    .Set(x => x.GoodId, item.GoodId)
                    .Set(x => x.MeasureId, item.MeasureId)
                    .Set(x => x.IsBase, item.Base)
                    .Set(x => x.Coefficient, item.Coefficient)
                    .Set(x => x.MarkingPack, item.MarkingPack)
                    .Set(x => x.BusinessRuUpdatedAt, item.Updated)
                    .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                // Check if FKs exist?
                // For simplicity, we assume they do or DB will throw. 
                // We're inside a bigger flow. Or we can use "InsertOrUpdate" behavior from library?
                // We'll trust the API consistency mostly.
                
                try 
                {
                    await _db.InsertAsync(new GoodsMeasure
                    {
                        Id = id,
                        GoodId = item.GoodId,
                        MeasureId = item.MeasureId,
                        IsBase = item.Base,
                        Coefficient = item.Coefficient,
                        MarkingPack = item.MarkingPack,
                        BusinessRuUpdatedAt = item.Updated,
                        LastSyncedAt = DateTimeOffset.UtcNow
                    }, token: ct);
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Ошибка при вставке GoodsMeasure ID={Id}. Возможно, отсутствует товар или единица измерения.", id);
                }
            }
        }
        _logger.LogInformation("Синхронизация отношений завершена.");
    }
    public async Task SyncPriceTypesAsync(CancellationToken ct = default)

    {
        _logger.LogInformation("Начинаем синхронизацию типов цен (price types)...");
        var items = await _client.GetPriceTypesAsync(cancellationToken: ct);
        _logger.LogInformation("Получено {Count} типов цен.", items.Length);

        foreach (var item in items)
        {
            var id = item.Id;
            var existing = await _db.PriceTypes.FirstOrDefaultAsync(x => x.Id == id, ct);

            // CurrencyId is already long? in model
            var currencyId = item.CurrencyId;

            if (existing != null)
            {
                await _db.PriceTypes
                    .Where(x => x.Id == id)
                    .Set(x => x.Name, item.Name)
                    .Set(x => x.CurrencyId, currencyId)
                    .Set(x => x.IsArchive, item.Archive)
                    .Set(x => x.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new PriceType
                {
                    Id = id,
                    Name = item.Name,
                    CurrencyId = currencyId,
                    IsArchive = item.Archive,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }
        _logger.LogInformation("Синхронизация типов цен завершена.");
    }
}
