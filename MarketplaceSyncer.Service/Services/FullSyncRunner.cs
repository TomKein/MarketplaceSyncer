using MarketplaceSyncer.Service.Configuration;
using Microsoft.Extensions.Options;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Исполнитель полной синхронизации с явными чекпоинтами.
/// Поддерживает crash recovery - продолжает с того места, где прервались.
/// </summary>
public class FullSyncRunner(
    SyncStateRepository state,
    ReferenceSyncer references,
    AttributeSyncer attributes,
    GoodsSyncer goods,
    IOptions<SynchronizationOptions> options,
    ILogger<FullSyncRunner> logger)
{
    private readonly SynchronizationOptions _options = options.Value;

    /// <summary>
    /// Проверить, есть ли незавершенная полная синхронизация
    /// </summary>
    public Task<bool> IsInProgressAsync(CancellationToken ct = default)
        => state.GetBoolAsync(SyncStateKeys.FullInProgress, false, ct);

    /// <summary>
    /// Получить время последней завершенной полной синхронизации
    /// </summary>
    public Task<DateTimeOffset?> GetLastCompletedAtAsync(CancellationToken ct = default)
        => state.GetDateTimeOffsetAsync(SyncStateKeys.FullCompletedAt, ct);

    /// <summary>
    /// Проверить, нужна ли полная синхронизация (прошло больше FullSyncMaxAge)
    /// </summary>
    public async Task<bool> IsFullSyncNeededAsync(CancellationToken ct = default)
    {
        var lastCompleted = await GetLastCompletedAtAsync(ct);
        if (lastCompleted == null)
            return true; // Никогда не синхронизировались

        return DateTimeOffset.UtcNow - lastCompleted.Value >= _options.FullSyncMaxAge;
    }

    /// <summary>
    /// Сбросить прогресс полной синхронизации (перед началом нового цикла)
    /// </summary>
    public async Task ResetProgressAsync(CancellationToken ct = default)
    {
        await state.SetBoolAsync(SyncStateKeys.FullInProgress, true, ct);
        await state.SetDateTimeOffsetAsync(SyncStateKeys.FullStartedAt, DateTimeOffset.UtcNow, ct);

        await state.SetBoolAsync(SyncStateKeys.FullDictionariesComplete, false, ct);
        await state.SetBoolAsync(SyncStateKeys.FullAttributesComplete, false, ct);
        await state.SetBoolAsync(SyncStateKeys.FullRelationsComplete, false, ct);

        await state.SetBoolAsync(SyncStateKeys.FullGoodsComplete, false, ct);
        await state.SetIntAsync(SyncStateKeys.FullGoodsPage, 1, ct);
        await state.SetIntAsync(SyncStateKeys.FullGoodsTotalPages, 0, ct);

        logger.LogDebug("Прогресс полной синхронизации сброшен для нового цикла");
    }

    /// <summary>
    /// Запустить полную синхронизацию с чекпоинтами.
    /// Возвращает true, если синхронизация завершена, false если была прервана.
    /// </summary>
    public async Task<bool> RunAsync(Func<bool> shouldStop, CancellationToken ct = default)
    {
        logger.LogInformation("========== ПОЛНАЯ СИНХРОНИЗАЦИЯ ==========");

        // Step 1: Справочники (Группы, Страны, Валюты, Единицы)
        if (!await state.GetBoolAsync(SyncStateKeys.FullDictionariesComplete, false, ct))
        {
            if (shouldStop()) return false;

            logger.LogInformation("[Step 1/4] Загрузка справочников (страны, валюты, единицы, группы)...");
            await references.SyncDictionariesAsync(ct);
            await state.SetBoolAsync(SyncStateKeys.FullDictionariesComplete, true, ct);
            logger.LogInformation("[Step 1/4] Справочники загружены");
        }
        else
        {
            logger.LogInformation("[Step 1/4] Справочники уже загружены, пропускаем");
        }

        // Step 2: Атрибуты
        if (!await state.GetBoolAsync(SyncStateKeys.FullAttributesComplete, false, ct))
        {
            if (shouldStop()) return false;

            logger.LogInformation("[Step 2/4] Загрузка атрибутов и значений...");
            await attributes.SyncAttributesAndValuesAsync(ct);
            await state.SetBoolAsync(SyncStateKeys.FullAttributesComplete, true, ct);
            logger.LogInformation("[Step 2/4] Атрибуты загружены");
        }
        else
        {
            logger.LogInformation("[Step 2/4] Атрибуты уже загружены, пропускаем");
        }

        // Step 3: Товары (с пагинацией, включая inline-скачивание изображений)
        if (!await state.GetBoolAsync(SyncStateKeys.FullGoodsComplete, false, ct))
        {
            var completed = await SyncGoodsAsync(shouldStop, ct);
            if (!completed) return false;
        }
        else
        {
            logger.LogInformation("[Step 3/4] Товары уже загружены, пропускаем");
        }

        // Step 4: Отношения (GoodsMeasures) - ПОСЛЕ товаров
        if (!await state.GetBoolAsync(SyncStateKeys.FullRelationsComplete, false, ct))
        {
            if (shouldStop()) return false;

            logger.LogInformation("[Step 4/4] Загрузка отношений (GoodsMeasures)...");
            await references.SyncGoodsRelationsAsync(ct);
            await state.SetBoolAsync(SyncStateKeys.FullRelationsComplete, true, ct);
            logger.LogInformation("[Step 4/4] Отношения загружены");
        }
        else
        {
            logger.LogInformation("[Step 4/4] Отношения уже загружены, пропускаем");
        }

        // Завершение
        await state.SetBoolAsync(SyncStateKeys.FullInProgress, false, ct);
        await state.SetDateTimeOffsetAsync(SyncStateKeys.FullCompletedAt, DateTimeOffset.UtcNow, ct);
        await state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTimeOffset.UtcNow, ct);

        logger.LogInformation("========== ПОЛНАЯ СИНХРОНИЗАЦИЯ ЗАВЕРШЕНА ==========");
        return true;
    }

    private async Task<bool> SyncGoodsAsync(Func<bool> shouldStop, CancellationToken ct)
    {
        logger.LogInformation("[Step 3/4] Загрузка товаров (включая изображения)...");

        // Инициализация общего количества страниц (если ещё не сделано)
        var totalPages = await state.GetIntAsync(SyncStateKeys.FullGoodsTotalPages, 0, ct);
        if (totalPages == 0)
        {
            var totalCount = await goods.GetTotalCountAsync(ct);
            totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
            await state.SetIntAsync(SyncStateKeys.FullGoodsTotalPages, totalPages, ct);
            logger.LogInformation("[Step 3/4] Всего товаров: {Count}, страниц: {Pages}", totalCount, totalPages);
        }

        // Получаем текущую страницу (начинаем с 1)
        var currentPage = await state.GetIntAsync(SyncStateKeys.FullGoodsPage, 1, ct);

        while (currentPage <= totalPages)
        {
            if (shouldStop()) return false;

            ct.ThrowIfCancellationRequested();

            logger.LogInformation("[Step 3/4] Страница {Page}/{Total}...", currentPage, totalPages);

            await goods.LoadAndSavePageAsync(currentPage, ct);

            // Сохраняем прогресс ПОСЛЕ успешной загрузки страницы
            currentPage++;
            await state.SetIntAsync(SyncStateKeys.FullGoodsPage, currentPage, ct);
        }

        await state.SetBoolAsync(SyncStateKeys.FullGoodsComplete, true, ct);
        logger.LogInformation("[Step 3/4] Все товары загружены ({Pages} страниц)", totalPages);
        return true;
    }
}
