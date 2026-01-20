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
    private readonly AttributeSyncer _attributes;
    private readonly GoodsSyncer _goods;
    private readonly ImageSyncService _images;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<InitialSyncRunner> _logger;

    public InitialSyncRunner(
        SyncStateRepository state,
        ReferenceSyncer references,
        AttributeSyncer attributes,
        GoodsSyncer goods,
        ImageSyncService images,
        IOptions<SynchronizationOptions> options,
        ILogger<InitialSyncRunner> logger)
    {
        _state = state;
        _references = references;
        _attributes = attributes;
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
        
        await _state.SetBoolAsync(SyncStateKeys.InitialDictionariesComplete, false, ct);
        await _state.SetBoolAsync(SyncStateKeys.InitialAttributesComplete, false, ct);
        await _state.SetBoolAsync(SyncStateKeys.InitialRelationsComplete, false, ct);
        
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
        
        await _state.SetBoolAsync(SyncStateKeys.DailyDictionariesComplete, false, ct);
        await _state.SetBoolAsync(SyncStateKeys.DailyAttributesComplete, false, ct);
        await _state.SetBoolAsync(SyncStateKeys.DailyRelationsComplete, false, ct);
        
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
            dictionariesCompleteKey: SyncStateKeys.InitialDictionariesComplete,
            attributesCompleteKey: SyncStateKeys.InitialAttributesComplete,
            goodsCompleteKey: SyncStateKeys.InitialGoodsComplete,
            goodsPageKey: SyncStateKeys.InitialGoodsPage,
            goodsTotalPagesKey: SyncStateKeys.InitialGoodsTotalPages,
            relationsCompleteKey: SyncStateKeys.InitialRelationsComplete,
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
            dictionariesCompleteKey: SyncStateKeys.DailyDictionariesComplete,
            attributesCompleteKey: SyncStateKeys.DailyAttributesComplete,
            goodsCompleteKey: SyncStateKeys.DailyGoodsComplete,
            goodsPageKey: SyncStateKeys.DailyGoodsPage,
            goodsTotalPagesKey: SyncStateKeys.DailyGoodsTotalPages,
            relationsCompleteKey: SyncStateKeys.DailyRelationsComplete,
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
        string dictionariesCompleteKey,
        string attributesCompleteKey,
        string goodsCompleteKey,
        string goodsPageKey,
        string goodsTotalPagesKey,
        string relationsCompleteKey,
        string imagesCompleteKey,
        string imagesIndexKey,
        string finalCompleteKey,
        bool isDaily,
        CancellationToken ct)
    {
        // Step 1: Справочники (Группы, Страны, Валюты, Единицы)
        if (!await _state.GetBoolAsync(dictionariesCompleteKey, false, ct))
        {
            _logger.LogInformation("[Step 1/5] Загрузка справочников (страны, валюты, единицы, группы)...");
            await _references.SyncDictionariesAsync(ct);
            await _state.SetBoolAsync(dictionariesCompleteKey, true, ct);
            _logger.LogInformation("[Step 1/5] ✓ Справочники загружены");
        }
        else
        {
            _logger.LogInformation("[Step 1/5] ✓ Справочники уже загружены, пропускаем");
        }

        // Step 2: Атрибуты
        if (!await _state.GetBoolAsync(attributesCompleteKey, false, ct))
        {
            _logger.LogInformation("[Step 2/5] Загрузка атрибутов и значений...");
            await _attributes.SyncAttributesAndValuesAsync(ct);
            await _state.SetBoolAsync(attributesCompleteKey, true, ct);
            _logger.LogInformation("[Step 2/5] ✓ Атрибуты загружены");
        }
        else
        {
            _logger.LogInformation("[Step 2/5] ✓ Атрибуты уже загружены, пропускаем");
        }

        // Step 3: Товары (с пагинацией)
        if (!await _state.GetBoolAsync(goodsCompleteKey, false, ct))
        {
            await SyncGoodsGenericAsync(goodsPageKey, goodsTotalPagesKey, goodsCompleteKey, ct);
        }
        else
        {
            _logger.LogInformation("[Step 3/5] ✓ Товары уже загружены, пропускаем");
        }

        // Step 4: Отношения (GoodsMeasures) - ПОСЛЕ товаров
        if (!await _state.GetBoolAsync(relationsCompleteKey, false, ct))
        {
            _logger.LogInformation("[Step 4/5] Загрузка отношений (GoodsMeasures)...");
            await _references.SyncGoodsRelationsAsync(ct);
            await _state.SetBoolAsync(relationsCompleteKey, true, ct);
            _logger.LogInformation("[Step 4/5] ✓ Отношения загружены");
        }
        else
        {
            _logger.LogInformation("[Step 4/5] ✓ Отношения уже загружены, пропускаем");
        }

        // Step 5: Изображения
        if (!await _state.GetBoolAsync(imagesCompleteKey, false, ct))
        {
            await SyncImagesGenericAsync(imagesIndexKey, imagesCompleteKey, ct);
        }
        else
        {
            _logger.LogInformation("[Step 5/5] ✓ Изображения уже загружены, пропускаем");
        }

        // Финальный флаг
        await _state.SetBoolAsync(finalCompleteKey, true, ct);
        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTimeOffset.UtcNow, ct);
    }

    private async Task SyncGoodsGenericAsync(string pageKey, string totalPagesKey, string completeKey, CancellationToken ct)
    {
        _logger.LogInformation("[Step 3/5] Загрузка товаров...");

        // Инициализация общего количества страниц (если ещё не сделано)
        var totalPages = await _state.GetIntAsync(totalPagesKey, 0, ct);
        if (totalPages == 0)
        {
            var totalCount = await _goods.GetTotalCountAsync(ct);
            totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
            await _state.SetIntAsync(totalPagesKey, totalPages, ct);
            _logger.LogInformation("[Step 3/5] Всего товаров: {Count}, страниц: {Pages}", totalCount, totalPages);
        }

        // Получаем текущую страницу (начинаем с 1)
        var currentPage = await _state.GetIntAsync(pageKey, 1, ct);
        
        while (currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("[Step 3/5] Страница {Page}/{Total}...", currentPage, totalPages);
            
            await _goods.LoadAndSavePageAsync(currentPage, ct);
            
            // Сохраняем прогресс ПОСЛЕ успешной загрузки страницы
            currentPage++;
            await _state.SetIntAsync(pageKey, currentPage, ct);
        }

        await _state.SetBoolAsync(completeKey, true, ct);
        _logger.LogInformation("[Step 3/5] ✓ Все товары загружены ({Pages} страниц)", totalPages);
    }

    private async Task SyncImagesGenericAsync(string indexKey, string completeKey, CancellationToken ct)
    {
        _logger.LogInformation("[Step 5/5] Загрузка изображений...");

        // Получаем список ID товаров из БД
        var goodIds = await _goods.GetAllGoodIdsAsync(ct);
        var totalGoods = goodIds.Count;
        
        // Получаем индекс, с которого продолжаем
        var startIndex = await _state.GetIntAsync(indexKey, 0, ct);
        
        _logger.LogInformation("[Step 5/5] Товаров: {Total}, начинаем с индекса: {Index}", 
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
                _logger.LogWarning(ex, "[Step 5/5] Ошибка загрузки изображений для товара {GoodId}, пропускаем", goodId);
            }

            // Сохраняем прогресс каждые 10 товаров
            if ((i + 1) % 10 == 0)
            {
                await _state.SetIntAsync(indexKey, i + 1, ct);
                _logger.LogDebug("[Step 5/5] Прогресс: {Current}/{Total}", i + 1, totalGoods);
            }
        }

        await _state.SetBoolAsync(completeKey, true, ct);
        _logger.LogInformation("[Step 5/5] ✓ Все изображения загружены ({Count} товаров)", totalGoods);
    }
}
