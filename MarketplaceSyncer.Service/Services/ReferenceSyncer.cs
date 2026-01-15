using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
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

        foreach (var apiGroup in apiGroups)
        {
            var id = apiGroup.Id;
            var existing = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct);

            if (existing != null)
            {
                await _db.Groups
                    .Where(g => g.Id == id)
                    .Set(g => g.Name, apiGroup.Name)
                    .Set(g => g.ParentId, apiGroup.ParentId)
                    .Set(g => g.LastSyncedAt, DateTime.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Group
                {
                    Id = id,
                    Name = apiGroup.Name ?? "",
                    ParentId = apiGroup.ParentId,
                    LastSyncedAt = DateTime.UtcNow
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
                    .Set(u => u.LastSyncedAt, DateTime.UtcNow)
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
                    LastSyncedAt = DateTime.UtcNow
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
