using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор справочников (группы, единицы измерения)
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
    /// Синхронизировать группы товаров
    /// </summary>
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

            // Добавляем дочерние элементы
            foreach (var child in lookup[current.Id])
            {
                queue.Enqueue(child);
            }
        }

        // 3. Если есть сироты (битые ссылки или циклы), добавляем их в конец
        if (sortedGroups.Count < apiGroups.Length)
        {
            var processedIds = sortedGroups.Select(g => g.Id).ToHashSet();
            var orphans = apiGroups.Where(g => !processedIds.Contains(g.Id)).ToList();
            _logger.LogWarning("Обнаружено {Count} изолированных групп или циклов. Добавляем в конец.", orphans.Count);
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

        _logger.LogInformation("Синхронизация групп завершена: {Count} записей", apiGroups.Length);
    }

    /// <summary>
    /// Синхронизировать единицы измерения
    /// </summary>
    public async Task SyncUnitsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем синхронизацию единиц измерения...");
        
        var apiUnits = await _client.GetUnitsAsync(ct);
        _logger.LogInformation("Получено {Count} единиц из API", apiUnits.Length);

        foreach (var apiUnit in apiUnits)
        {
            var id = apiUnit.Id;
            var existing = await _db.Units.FirstOrDefaultAsync(u => u.Id == id, ct);

            if (existing != null)
            {
                await _db.Units
                    .Where(u => u.Id == id)
                    .Set(u => u.Name, apiUnit.Name)
                    .Set(u => u.FullName, apiUnit.FullName)
                    .Set(u => u.Code, apiUnit.Code)
                    .Set(u => u.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Unit
                {
                    Id = id,
                    Name = apiUnit.Name ?? "",
                    FullName = apiUnit.FullName,
                    Code = apiUnit.Code,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }

        _logger.LogInformation("Синхронизация единиц завершена: {Count} записей", apiUnits.Length);
    }

    /// <summary>
    /// Полная синхронизация всех справочников
    /// </summary>
    public async Task RunFullSyncAsync(CancellationToken ct = default)
    {
        await SyncGroupsAsync(ct);
        await SyncUnitsAsync(ct);
    }
}
