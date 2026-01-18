using MarketplaceSyncer.Service.Configuration;
using Microsoft.Extensions.Options;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Исполнитель инициальной и ежедневной полной синхронизации с явными чекпоинтами.
/// Поддерживает crash recovery — продолжает с того места, где прервались.
/// </summary>
public class InitialSyncRunner
{
    private readonly SyncStateRepository _state;
    private readonly ReferenceSyncer _references;
    private readonly GoodsSyncer _goods;
    private readonly ImageSyncService _images;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<InitialSyncRunner> _logger;

    public InitialSyncRunner(
        SyncStateRepository state,
        ReferenceSyncer references,
        GoodsSyncer goods,
        ImageSyncService images,
        IOptions<SynchronizationOptions> options,
        ILogger<InitialSyncRunner> logger)
    {
        _state = state;
        _references = references;
        _goods = goods;
        _images = images;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Проверить, завершена ли инициальная синхронизация
    /// </summary>
    public Task<bool> IsCompleteAsync(CancellationToken ct = default)
        => _state.GetBoolAsync(SyncStateKeys.InitialComplete, false, ct);

    /// <summary>
    /// Сбросить прогресс ИНИЦИАЛЬНОЙ синхронизации (только для отладки или полного сброса).
    /// </summary>
    public async Task ResetProgressAsync(CancellationToken ct = default)
    {
        await _state.SetBoolAsync(SyncStateKeys.InitialComplete, false, ct);
        
        await _state.SetBoolAsync(SyncStateKeys.InitialGroupsComplete, false, ct);
        await _state.SetBoolAsync(SyncStateKeys.InitialUnitsComplete, false, ct);
        
        await _state.SetBoolAsync(SyncStateKeys.InitialGoodsComplete, false, ct);
        await _state.SetIntAsync(SyncStateKeys.InitialGoodsPage, 1, ct);
        await _state.SetIntAsync(SyncStateKeys.InitialGoodsTotalPages, 0, ct);

        await _state.SetBoolAsync(SyncStateKeys.InitialImagesComplete, false, ct);
        await _state.SetIntAsync(SyncStateKeys.InitialImagesGoodIndex, 0, ct);

        _logger.LogWarning("⚠️ Прогресс ИНИЦИАЛЬНОЙ синхронизации сброшен");
    }
    
    /// <summary>
    /// Сбросить прогресс ЕЖЕДНЕВНОЙ синхронизации (перед началом нового цикла).
    /// </summary>
    public async Task ResetDailyProgressAsync(CancellationToken ct = default)
    {
        await _state.SetBoolAsync(SyncStateKeys.DailyComplete, false, ct);
        
        await _state.SetBoolAsync(SyncStateKeys.DailyGroupsComplete, false, ct);
        await _state.SetBoolAsync(SyncStateKeys.DailyUnitsComplete, false, ct);
        
        await _state.SetBoolAsync(SyncStateKeys.DailyGoodsComplete, false, ct);
        await _state.SetIntAsync(SyncStateKeys.DailyGoodsPage, 1, ct);
        await _state.SetIntAsync(SyncStateKeys.DailyGoodsTotalPages, 0, ct);

        await _state.SetBoolAsync(SyncStateKeys.DailyImagesComplete, false, ct);
        await _state.SetIntAsync(SyncStateKeys.DailyImagesGoodIndex, 0, ct);
        
        _logger.LogDebug("Прогресс Ежедневной синхронизации сброшен для нового цикла");
    }

    /// <summary>
    /// Запустить ИНИЦИАЛЬНУЮ синхронизацию с чекпоинтами (Initial_* ключи).
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("========== ИНИЦИАЛЬНАЯ СИНХРОНИЗАЦИЯ ==========");

        await RunGenericSyncAsync(
            groupsCompleteKey: SyncStateKeys.InitialGroupsComplete,
            unitsCompleteKey: SyncStateKeys.InitialUnitsComplete,
            goodsCompleteKey: SyncStateKeys.InitialGoodsComplete,
            goodsPageKey: SyncStateKeys.InitialGoodsPage,
            goodsTotalPagesKey: SyncStateKeys.InitialGoodsTotalPages,
            imagesCompleteKey: SyncStateKeys.InitialImagesComplete,
            imagesIndexKey: SyncStateKeys.InitialImagesGoodIndex,
            finalCompleteKey: SyncStateKeys.InitialComplete,
            isDaily: false,
            ct: ct);
            
        _logger.LogInformation("========== ИНИЦИАЛЬНАЯ СИНХРОНИЗАЦИЯ ЗАВЕРШЕНА ==========");
    }

    /// <summary>
    /// Запустить ЕЖЕДНЕВНУЮ полную синхронизацию (Daily_* ключи).
    /// </summary>
    public async Task RunDailyAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("========== ЕЖЕДНЕВНАЯ (FULL) СИНХРОНИЗАЦИЯ ==========");

        await RunGenericSyncAsync(
            groupsCompleteKey: SyncStateKeys.DailyGroupsComplete,
            unitsCompleteKey: SyncStateKeys.DailyUnitsComplete,
            goodsCompleteKey: SyncStateKeys.DailyGoodsComplete,
            goodsPageKey: SyncStateKeys.DailyGoodsPage,
            goodsTotalPagesKey: SyncStateKeys.DailyGoodsTotalPages,
            imagesCompleteKey: SyncStateKeys.DailyImagesComplete,
            imagesIndexKey: SyncStateKeys.DailyImagesGoodIndex,
            finalCompleteKey: SyncStateKeys.DailyComplete,
            isDaily: true,
            ct: ct);
        
        // Отмечаем время завершения Daily
        await _state.SetLastRunAsync(SyncStateKeys.DailyFullResyncLastRun, DateTimeOffset.UtcNow, ct);
        
        _logger.LogInformation("========== ЕЖЕДНЕВНАЯ СИНХРОНИЗАЦИЯ ЗАВЕРШЕНА ==========");
    }

    private async Task RunGenericSyncAsync(
        string groupsCompleteKey,
        string unitsCompleteKey,
        string goodsCompleteKey,
        string goodsPageKey,
        string goodsTotalPagesKey,
        string imagesCompleteKey,
        string imagesIndexKey,
        string finalCompleteKey,
        bool isDaily,
        CancellationToken ct)
    {
        // Step 1: Группы
        if (!await _state.GetBoolAsync(groupsCompleteKey, false, ct))
        {
            _logger.LogInformation("[Step 1/4] Загрузка групп...");
            await _references.SyncGroupsAsync(ct);
            await _state.SetBoolAsync(groupsCompleteKey, true, ct);
            _logger.LogInformation("[Step 1/4] ✓ Группы загружены");
        }
        else
        {
            _logger.LogInformation("[Step 1/4] ✓ Группы уже загружены, пропускаем");
        }

        // Step 2: Единицы
        if (!await _state.GetBoolAsync(unitsCompleteKey, false, ct))
        {
            _logger.LogInformation("[Step 2/4] Загрузка единиц измерения...");
            await _references.SyncUnitsAsync(ct);
            await _state.SetBoolAsync(unitsCompleteKey, true, ct);
            _logger.LogInformation("[Step 2/4] ✓ Единицы загружены");
        }
        else
        {
            _logger.LogInformation("[Step 2/4] ✓ Единицы уже загружены, пропускаем");
        }

        // Step 3: Товары (с пагинацией)
        if (!await _state.GetBoolAsync(goodsCompleteKey, false, ct))
        {
            await SyncGoodsGenericAsync(goodsPageKey, goodsTotalPagesKey, goodsCompleteKey, ct);
        }
        else
        {
            _logger.LogInformation("[Step 3/4] ✓ Товары уже загружены, пропускаем");
        }

        // Step 4: Изображения
        if (!await _state.GetBoolAsync(imagesCompleteKey, false, ct))
        {
            await SyncImagesGenericAsync(imagesIndexKey, imagesCompleteKey, ct);
        }
        else
        {
            _logger.LogInformation("[Step 4/4] ✓ Изображения уже загружены, пропускаем");
        }

        // Финальный флаг
        await _state.SetBoolAsync(finalCompleteKey, true, ct);
        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTimeOffset.UtcNow, ct);
    }

    private async Task SyncGoodsGenericAsync(string pageKey, string totalPagesKey, string completeKey, CancellationToken ct)
    {
        _logger.LogInformation("[Step 3/4] Загрузка товаров...");

        // Инициализация общего количества страниц (если ещё не сделано)
        var totalPages = await _state.GetIntAsync(totalPagesKey, 0, ct);
        if (totalPages == 0)
        {
            var totalCount = await _goods.GetTotalCountAsync(ct);
            totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
            await _state.SetIntAsync(totalPagesKey, totalPages, ct);
            _logger.LogInformation("[Step 3/4] Всего товаров: {Count}, страниц: {Pages}", totalCount, totalPages);
        }

        // Получаем текущую страницу (начинаем с 1)
        var currentPage = await _state.GetIntAsync(pageKey, 1, ct);
        
        while (currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("[Step 3/4] Страница {Page}/{Total}...", currentPage, totalPages);
            
            await _goods.LoadAndSavePageAsync(currentPage, ct);
            
            // Сохраняем прогресс ПОСЛЕ успешной загрузки страницы
            currentPage++;
            await _state.SetIntAsync(pageKey, currentPage, ct);
        }

        await _state.SetBoolAsync(completeKey, true, ct);
        _logger.LogInformation("[Step 3/4] ✓ Все товары загружены ({Pages} страниц)", totalPages);
    }

    private async Task SyncImagesGenericAsync(string indexKey, string completeKey, CancellationToken ct)
    {
        _logger.LogInformation("[Step 4/4] Загрузка изображений...");

        // Получаем список ID товаров из БД
        var goodIds = await _goods.GetAllGoodIdsAsync(ct);
        var totalGoods = goodIds.Count;
        
        // Получаем индекс, с которого продолжаем
        var startIndex = await _state.GetIntAsync(indexKey, 0, ct);
        
        _logger.LogInformation("[Step 4/4] Товаров: {Total}, начинаем с индекса: {Index}", 
            totalGoods, startIndex);

        for (var i = startIndex; i < totalGoods; i++)
        {
            ct.ThrowIfCancellationRequested();

            var goodId = goodIds[i];
            
            try
            {
                await _images.SyncGoodImagesAsync(goodId, goodId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Step 4/4] Ошибка загрузки изображений для товара {GoodId}, пропускаем", goodId);
            }

            // Сохраняем прогресс каждые 10 товаров
            if ((i + 1) % 10 == 0)
            {
                await _state.SetIntAsync(indexKey, i + 1, ct);
                _logger.LogDebug("[Step 4/4] Прогресс: {Current}/{Total}", i + 1, totalGoods);
            }
        }

        await _state.SetBoolAsync(completeKey, true, ct);
        _logger.LogInformation("[Step 4/4] ✓ Все изображения загружены ({Count} товаров)", totalGoods);
    }
}
