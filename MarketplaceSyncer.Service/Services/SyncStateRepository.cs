using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.Data;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Репозиторий для управления состоянием процессов синхронизации.
/// <para>
/// Предоставляет абстракцию над таблицей `sync_state` для чтения и записи контрольных точек (checkpoints),
/// флагов завершения этапов и временных меток последних запусков.
/// </para>
/// <para>
/// Используется бизнес-логикой (синкерами) для обеспечения идемпотентности и возможности продолжения работы (resumability)
/// в случае перезапуска приложения.
/// </para>
/// </summary>
public class SyncStateRepository
{
    private readonly AppDataConnection _db;
    private readonly ILogger<SyncStateRepository> _logger;

    public SyncStateRepository(AppDataConnection db, ILogger<SyncStateRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить значение по ключу
    /// </summary>
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.SyncState
            .Where(s => s.Key == key)
            .FirstOrDefaultAsync(ct);
        return setting?.Value;
    }

    /// <summary>
    /// Установить значение по ключу
    /// </summary>
    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var existing = await _db.SyncState
            .Where(s => s.Key == key)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            await _db.SyncState
                .Where(s => s.Key == key)
                .Set(s => s.Value, value)
                .Set(s => s.UpdatedAt, DateTimeOffset.UtcNow)
                .UpdateAsync(ct);
        }
        else
        {
            await _db.InsertAsync(new Data.Models.SyncState
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTimeOffset.UtcNow
            }, token: ct);
        }
    }

    /// <summary>
    /// Получить DateTimeOffset по ключу
    /// </summary>
    public async Task<DateTimeOffset?> GetDateTimeOffsetAsync(string key, CancellationToken ct = default)
    {
        var value = await GetAsync(key, ct);
        if (string.IsNullOrEmpty(value)) return null;
        return DateTimeOffset.TryParse(value, out var dt) ? dt : null;
    }

    /// <summary>
    /// Установить DateTimeOffset по ключу
    /// </summary>
    public async Task SetDateTimeOffsetAsync(string key, DateTimeOffset value, CancellationToken ct = default)
    {
        await SetAsync(key, value.ToString("O"), ct);
    }

    /// <summary>
    /// Получить int по ключу
    /// </summary>
    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var value = await GetAsync(key, ct);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return int.TryParse(value, out var i) ? i : defaultValue;
    }

    /// <summary>
    /// Установить int по ключу
    /// </summary>
    public async Task SetIntAsync(string key, int value, CancellationToken ct = default)
    {
        await SetAsync(key, value.ToString(), ct);
    }

    /// <summary>
    /// Получить bool по ключу
    /// </summary>
    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var value = await GetAsync(key, ct);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return bool.TryParse(value, out var b) ? b : defaultValue;
    }

    /// <summary>
    /// Установить bool по ключу
    /// </summary>
    public async Task SetBoolAsync(string key, bool value, CancellationToken ct = default)
    {
        await SetAsync(key, value.ToString(), ct);
    }

    // ========== Специализированные методы ==========

    /// <summary>
    /// Получить время последнего запуска задачи
    /// </summary>
    public Task<DateTimeOffset?> GetLastRunAsync(string taskKey, CancellationToken ct = default)
        => GetDateTimeOffsetAsync(taskKey, ct);

    /// <summary>
    /// Установить время последнего запуска задачи
    /// </summary>
    public Task SetLastRunAsync(string taskKey, DateTimeOffset time, CancellationToken ct = default)
        => SetDateTimeOffsetAsync(taskKey, time, ct);
}

/// <summary>
/// Константы ключей для sync_state
/// </summary>
public static class SyncStateKeys
{
    // ========== Полная синхронизация (Full Sync) ==========

    /// <summary>Флаг: полная синхронизация в процессе</summary>
    public const string FullInProgress = "Full_InProgress";

    /// <summary>Время начала текущей полной синхронизации</summary>
    public const string FullStartedAt = "Full_StartedAt";

    /// <summary>Время завершения последней полной синхронизации</summary>
    public const string FullCompletedAt = "Full_CompletedAt";

    /// <summary>Флаг: справочники (страны, валюты, ед. изм, группы) загружены</summary>
    public const string FullDictionariesComplete = "Full_Dictionaries_Complete";

    /// <summary>Флаг: атрибуты загружены</summary>
    public const string FullAttributesComplete = "Full_Attributes_Complete";

    /// <summary>Флаг: связи товаров и единиц измерения загружены</summary>
    public const string FullRelationsComplete = "Full_Relations_Complete";

    /// <summary>Текущая страница товаров (1-based)</summary>
    public const string FullGoodsPage = "Full_Goods_Page";

    /// <summary>Всего страниц товаров</summary>
    public const string FullGoodsTotalPages = "Full_Goods_TotalPages";

    /// <summary>Флаг: все товары загружены (включая изображения)</summary>
    public const string FullGoodsComplete = "Full_Goods_Complete";

    // ========== Инкрементальная синхронизация ==========

    /// <summary>Время последней дельта-синхронизации товаров</summary>
    public const string GoodsLastDelta = "Sync_Goods_LastDelta";

    /// <summary>Время последней синхронизации цен (currentprices)</summary>
    public const string PricesLastDelta = "Sync_Prices_LastDelta";

    /// <summary>Время последней синхронизации остатков (storegoods)</summary>
    public const string StockLastDelta = "Sync_Stock_LastDelta";

    /// <summary>Время последней синхронизации справочников</summary>
    public const string ReferencesLastRun = "Sync_References_LastRun";
}
