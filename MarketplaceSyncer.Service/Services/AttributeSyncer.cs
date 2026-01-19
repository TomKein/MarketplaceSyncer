using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using Attribute = MarketplaceSyncer.Service.Data.Models.Attribute;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор атрибутов и их значений.
/// </summary>
public class AttributeSyncer
{
    private readonly IBusinessRuClient _client;
    private readonly AppDataConnection _db;
    private readonly ILogger<AttributeSyncer> _logger;

    public AttributeSyncer(
        IBusinessRuClient client,
        AppDataConnection db,
        ILogger<AttributeSyncer> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Полная синхронизация атрибутов и их значений.
    /// </summary>
    public async Task SyncAttributesAndValuesAsync(CancellationToken ct = default)
    {
        await SyncAttributesAsync(ct);
        await SyncAttributeValuesAsync(ct);
    }

    private async Task SyncAttributesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Начинаем синхронизацию атрибутов...");
        
        var apiAttributes = await _client.GetAttributesAsync(ct);
        _logger.LogInformation("Получено {Count} атрибутов из API", apiAttributes.Length);

        // Используем транзакцию или батчинг при большом количестве? Обычно атрибутов не тысячи, так что foreach ок.
        foreach (var apiAttr in apiAttributes)
        {
            var id = apiAttr.Id;
            var existing = await _db.Attributes.FirstOrDefaultAsync(a => a.Id == id, ct);

            if (existing != null)
            {
                await _db.Attributes
                    .Where(a => a.Id == id)
                    .Set(a => a.Name, apiAttr.Name)
                    .Set(a => a.Selectable, apiAttr.Selectable)
                    .Set(a => a.Archive, apiAttr.Archive)
                    .Set(a => a.Description, apiAttr.Description)
                    .Set(a => a.Sort, apiAttr.Sort)
                    .Set(a => a.Deleted, apiAttr.Deleted)
                    .Set(a => a.BusinessRuUpdatedAt, apiAttr.Updated)
                    .Set(a => a.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new Attribute
                {
                    Id = id,
                    Name = apiAttr.Name ?? "",
                    Selectable = apiAttr.Selectable,
                    Archive = apiAttr.Archive,
                    Description = apiAttr.Description,
                    Sort = apiAttr.Sort,
                    Deleted = apiAttr.Deleted,
                    BusinessRuUpdatedAt = apiAttr.Updated,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
        }

        _logger.LogInformation("Синхронизация атрибутов завершена.");
    }

    private async Task SyncAttributeValuesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Начинаем синхронизацию значений атрибутов...");
        
        var apiValues = await _client.GetAttributeValuesAsync(attributeId: null, cancellationToken: ct);
        _logger.LogInformation("Получено {Count} значений атрибутов из API", apiValues.Length);
        
        // Значений может быть много. Но пока foreach тоже ок.
        // Важно: если атрибута нет в базе (например, он удален в API, но значения пришли - странная ситуация, но возможная),
        // FK constraint упадет. Но мы только что синхронизировали атрибуты, так что должно быть ок.
        // Однако, если в values есть reference на удаленный или несуществующий атрибут, будет ошибка.
        // Проверим наличие атрибутов в кэше или просто try/catch? 
        // Лучше: получить все ID атрибутов из БД
        
        var existingAttributeIds = await _db.Attributes.Select(a => a.Id).ToListAsync(ct);
        var existingAttributeIdsSet = new HashSet<long>(existingAttributeIds);

        int skippedCount = 0;
        int processedCount = 0;

        foreach (var apiVal in apiValues)
        {
            if (!existingAttributeIdsSet.Contains(apiVal.AttributeId))
            {
                // Пропускаем значение, если нет родительского атрибута
                skippedCount++;
                continue;
            }

            var id = apiVal.Id;
            var existing = await _db.AttributeValues.FirstOrDefaultAsync(v => v.Id == id, ct);

            if (existing != null)
            {
                await _db.AttributeValues
                    .Where(v => v.Id == id)
                    .Set(v => v.AttributeId, apiVal.AttributeId)
                    .Set(v => v.Name, apiVal.Name)
                    .Set(v => v.Sort, apiVal.Sort)
                    .Set(v => v.BusinessRuUpdatedAt, apiVal.Updated)
                    .Set(v => v.LastSyncedAt, DateTimeOffset.UtcNow)
                    .UpdateAsync(ct);
            }
            else
            {
                await _db.InsertAsync(new AttributeValue
                {
                    Id = id,
                    AttributeId = apiVal.AttributeId,
                    Name = apiVal.Name ?? "",
                    Sort = apiVal.Sort,
                    BusinessRuUpdatedAt = apiVal.Updated,
                    LastSyncedAt = DateTimeOffset.UtcNow
                }, token: ct);
            }
            processedCount++;
        }

        if (skippedCount > 0)
        {
            _logger.LogWarning("Пропущено {Count} значений атрибутов, так как родительский атрибут не найден.", skippedCount);
        }

        _logger.LogInformation("Синхронизация значений атрибутов завершена. Обработано {Count}.", processedCount);
    }

}
