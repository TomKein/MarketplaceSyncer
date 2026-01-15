using MarketplaceSyncer.Service.Configuration;
using Microsoft.Extensions.Options;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Исполнитель инициальной синхронизации с явными чекпоинтами.
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
    /// Запустить инициальную синхронизацию с чекпоинтами.
    /// Продолжает с того места, где прервались в прошлый раз.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("========== ИНИЦИАЛЬНАЯ СИНХРОНИЗАЦИЯ ==========");

        // Step 1: Группы
        if (!await _state.GetBoolAsync(SyncStateKeys.InitialGroupsComplete, false, ct))
        {
            _logger.LogInformation("[Step 1/4] Загрузка групп...");
            await _references.SyncGroupsAsync(ct);
            await _state.SetBoolAsync(SyncStateKeys.InitialGroupsComplete, true, ct);
            _logger.LogInformation("[Step 1/4] ✓ Группы загружены");
        }
        else
        {
            _logger.LogInformation("[Step 1/4] ✓ Группы уже загружены, пропускаем");
        }

        // Step 2: Единицы
        if (!await _state.GetBoolAsync(SyncStateKeys.InitialUnitsComplete, false, ct))
        {
            _logger.LogInformation("[Step 2/4] Загрузка единиц измерения...");
            await _references.SyncUnitsAsync(ct);
            await _state.SetBoolAsync(SyncStateKeys.InitialUnitsComplete, true, ct);
            _logger.LogInformation("[Step 2/4] ✓ Единицы загружены");
        }
        else
        {
            _logger.LogInformation("[Step 2/4] ✓ Единицы уже загружены, пропускаем");
        }

        // Step 3: Товары (с пагинацией)
        if (!await _state.GetBoolAsync(SyncStateKeys.InitialGoodsComplete, false, ct))
        {
            await SyncGoodsWithPaginationAsync(ct);
        }
        else
        {
            _logger.LogInformation("[Step 3/4] ✓ Товары уже загружены, пропускаем");
        }

        // Step 4: Изображения
        if (!await _state.GetBoolAsync(SyncStateKeys.InitialImagesComplete, false, ct))
        {
            await SyncImagesForAllGoodsAsync(ct);
        }
        else
        {
            _logger.LogInformation("[Step 4/4] ✓ Изображения уже загружены, пропускаем");
        }

        // Финальный флаг
        await _state.SetBoolAsync(SyncStateKeys.InitialComplete, true, ct);
        await _state.SetDateTimeAsync(SyncStateKeys.GoodsLastDelta, DateTime.UtcNow, ct);
        
        _logger.LogInformation("========== ИНИЦИАЛЬНАЯ СИНХРОНИЗАЦИЯ ЗАВЕРШЕНА ==========");
    }

    /// <summary>
    /// Step 3: Загрузка товаров с пагинацией и чекпоинтами
    /// </summary>
    private async Task SyncGoodsWithPaginationAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Step 3/4] Загрузка товаров...");

        // Инициализация общего количества страниц (если ещё не сделано)
        var totalPages = await _state.GetIntAsync(SyncStateKeys.InitialGoodsTotalPages, 0, ct);
        if (totalPages == 0)
        {
            var totalCount = await _goods.GetTotalCountAsync(ct);
            totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
            await _state.SetIntAsync(SyncStateKeys.InitialGoodsTotalPages, totalPages, ct);
            _logger.LogInformation("[Step 3/4] Всего товаров: {Count}, страниц: {Pages}", totalCount, totalPages);
        }

        // Получаем текущую страницу (начинаем с 1)
        var currentPage = await _state.GetIntAsync(SyncStateKeys.InitialGoodsPage, 1, ct);
        
        while (currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("[Step 3/4] Страница {Page}/{Total}...", currentPage, totalPages);
            
            await _goods.LoadAndSavePageAsync(currentPage, ct);
            
            // Сохраняем прогресс ПОСЛЕ успешной загрузки страницы
            currentPage++;
            await _state.SetIntAsync(SyncStateKeys.InitialGoodsPage, currentPage, ct);
        }

        await _state.SetBoolAsync(SyncStateKeys.InitialGoodsComplete, true, ct);
        _logger.LogInformation("[Step 3/4] ✓ Все товары загружены ({Pages} страниц)", totalPages);
    }

    /// <summary>
    /// Step 4: Загрузка изображений для всех товаров
    /// </summary>
    private async Task SyncImagesForAllGoodsAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Step 4/4] Загрузка изображений...");

        // Получаем список ID товаров из БД
        var goodIds = await _goods.GetAllGoodIdsAsync(ct);
        var totalGoods = goodIds.Count;
        
        // Получаем индекс, с которого продолжаем
        var startIndex = await _state.GetIntAsync(SyncStateKeys.InitialImagesGoodIndex, 0, ct);
        
        _logger.LogInformation("[Step 4/4] Товаров: {Total}, начинаем с индекса: {Index}", 
            totalGoods, startIndex);

        for (var i = startIndex; i < totalGoods; i++)
        {
            ct.ThrowIfCancellationRequested();

            var goodId = goodIds[i];
            
            try
            {
                await _images.SyncGoodImagesAsync(goodId, goodId.ToString(), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Step 4/4] Ошибка загрузки изображений для товара {GoodId}, пропускаем", goodId);
            }

            // Сохраняем прогресс каждые 10 товаров
            if ((i + 1) % 10 == 0)
            {
                await _state.SetIntAsync(SyncStateKeys.InitialImagesGoodIndex, i + 1, ct);
                _logger.LogDebug("[Step 4/4] Прогресс: {Current}/{Total}", i + 1, totalGoods);
            }
        }

        await _state.SetBoolAsync(SyncStateKeys.InitialImagesComplete, true, ct);
        _logger.LogInformation("[Step 4/4] ✓ Все изображения загружены ({Count} товаров)", totalGoods);
    }
}
