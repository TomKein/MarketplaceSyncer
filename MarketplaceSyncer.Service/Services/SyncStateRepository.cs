using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.Data;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Репозиторий для хранения состояния синхронизации в таблице БД app_settings (ключ-значение)
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
                .Set(s => s.UpdatedAt, DateTime.UtcNow)
                .UpdateAsync(ct);
        }
        else
        {
            await _db.InsertAsync(new Data.Models.SyncState
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            }, token: ct);
        }
    }

    /// <summary>
    /// Получить DateTime по ключу
    /// </summary>
    public async Task<DateTime?> GetDateTimeAsync(string key, CancellationToken ct = default)
    {
        var value = await GetAsync(key, ct);
        if (string.IsNullOrEmpty(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt : null;
    }

    /// <summary>
    /// Установить DateTime по ключу
    /// </summary>
    public async Task SetDateTimeAsync(string key, DateTime value, CancellationToken ct = default)
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
    /// Проверить, завершена ли инициальная синхронизация
    /// </summary>
    public Task<bool> IsInitialSyncCompleteAsync(CancellationToken ct = default)
        => GetBoolAsync(SyncStateKeys.InitialComplete, false, ct);

    /// <summary>
    /// Отметить инициальную синхронизацию как завершённую
    /// </summary>
    public Task SetInitialSyncCompleteAsync(CancellationToken ct = default)
        => SetBoolAsync(SyncStateKeys.InitialComplete, true, ct);

    /// <summary>
    /// Получить время последнего запуска задачи
    /// </summary>
    public Task<DateTime?> GetLastRunAsync(string taskKey, CancellationToken ct = default)
        => GetDateTimeAsync(taskKey, ct);

    /// <summary>
    /// Установить время последнего запуска задачи
    /// </summary>
    public Task SetLastRunAsync(string taskKey, DateTime time, CancellationToken ct = default)
        => SetDateTimeAsync(taskKey, time, ct);
}

/// <summary>
/// Константы ключей для sync_state
/// </summary>
public static class SyncStateKeys
{
    // ========== Initial Sync Checkpoints ==========
    /// <summary>Флаг: группы загружены</summary>
    public const string InitialGroupsComplete = "Initial_Groups_Complete";
    
    /// <summary>Флаг: единицы загружены</summary>
    public const string InitialUnitsComplete = "Initial_Units_Complete";
    
    /// <summary>Текущая страница товаров (1-based)</summary>
    public const string InitialGoodsPage = "Initial_Goods_Page";
    
    /// <summary>Всего страниц товаров</summary>
    public const string InitialGoodsTotalPages = "Initial_Goods_TotalPages";
    
    /// <summary>Флаг: все товары загружены</summary>
    public const string InitialGoodsComplete = "Initial_Goods_Complete";
    
    /// <summary>Индекс текущего товара для загрузки изображений</summary>
    public const string InitialImagesGoodIndex = "Initial_Images_GoodIndex";
    
    /// <summary>Флаг: все изображения загружены</summary>
    public const string InitialImagesComplete = "Initial_Images_Complete";
    
    /// <summary>Флаг: вся инициальная синхронизация завершена</summary>
    public const string InitialComplete = "Initial_Complete";
    
    // ========== Incremental Sync ==========
    public const string GoodsLastDelta = "Sync_Goods_LastDelta";
    public const string ImagesLastDelta = "Sync_Images_LastDelta";
    public const string ReferencesLastRun = "Sync_References_LastRun";
    
    // ========== Full Reload Progress ==========
    public const string FullReloadGoodsCurrentPage = "FullReload_Goods_CurrentPage";
    public const string FullReloadGoodsTotalPages = "FullReload_Goods_TotalPages";
    public const string FullReloadGoodsStartedAt = "FullReload_Goods_StartedAt";
    public const string GoodsLastFull = "Sync_Goods_LastFull";
}
