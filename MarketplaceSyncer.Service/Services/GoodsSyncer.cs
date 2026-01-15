using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Configuration;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using Microsoft.Extensions.Options;
using ApiGood = MarketplaceSyncer.Service.BusinessRu.Models.Responses.Good;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Синхронизатор товаров с поддержкой пагинации и дозагрузки
/// </summary>
public class GoodsSyncer
{
    private readonly IBusinessRuClient _client;
    private readonly AppDataConnection _db;
    private readonly SyncStateRepository _state;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<GoodsSyncer> _logger;

    public GoodsSyncer(
        IBusinessRuClient client,
        AppDataConnection db,
        SyncStateRepository state,
        IOptions<SynchronizationOptions> options,
        ILogger<GoodsSyncer> logger)
    {
        _client = client;
        _db = db;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Инкрементальная синхронизация (MEDIUM приоритет)
    /// </summary>
    public async Task RunDeltaSyncAsync(CancellationToken ct = default)
    {
        var lastDelta = await _state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        var since = lastDelta ?? DateTime.UtcNow.AddDays(-1);

        _logger.LogInformation("Delta sync товаров с {Since}...", since);

        var changedGoods = await _client.GetGoodsChangedAfterAsync(since, cancellationToken: ct);
        _logger.LogInformation("Получено {Count} изменённых товаров", changedGoods.Length);

        foreach (var apiGood in changedGoods)
        {
            await UpsertGoodAsync(apiGood, ct);
        }

        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTime.UtcNow, ct);
        _logger.LogInformation("Delta sync завершён: {Count} товаров обработано", changedGoods.Length);
    }

    /// <summary>
    /// Инициальная загрузка с контролем страниц (HIGH приоритет)
    /// </summary>
    public async Task RunInitialSyncAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинаем инициальную загрузку товаров...");

        // Получаем общее количество для расчёта страниц
        var totalCount = await _client.CountGoodsAsync(cancellationToken: ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
        
        await _state.SetIntAsync(SyncStateKeys.InitialGoodsTotalPages, totalPages, ct);
        
        var currentPage = await _state.GetIntAsync(SyncStateKeys.InitialGoodsPage, 1, ct);
        _logger.LogInformation("Инициальная загрузка: страница {Current}/{Total}", currentPage, totalPages);

        while (currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var goods = await LoadGoodsPageAsync(currentPage, ct);
            _logger.LogInformation("Страница {Page}/{Total}: загружено {Count} товаров", 
                currentPage, totalPages, goods.Length);

            foreach (var apiGood in goods)
            {
                await UpsertGoodAsync(apiGood, ct);
            }

            currentPage++;
            await _state.SetIntAsync(SyncStateKeys.InitialGoodsPage, currentPage, ct);
        }

        // Сбрасываем прогресс
        await _state.SetIntAsync(SyncStateKeys.InitialGoodsPage, 1, ct);
        await _state.SetLastRunAsync(SyncStateKeys.GoodsLastDelta, DateTime.UtcNow, ct);
        
        _logger.LogInformation("Инициальная загрузка товаров завершена: {Total} страниц", totalPages);
    }

    /// <summary>
    /// Загрузка одного чанка для full reload (LOW приоритет)
    /// </summary>
    public async Task<bool> RunFullReloadChunkAsync(CancellationToken ct = default)
    {
        var currentPage = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, 1, ct);
        var totalPages = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, 0, ct);

        // Если totalPages = 0, нужно инициализировать
        if (totalPages == 0)
        {
            var totalCount = await _client.CountGoodsAsync(cancellationToken: ct);
            totalPages = (int)Math.Ceiling(totalCount / (double)_options.PageSize);
            await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, totalPages, ct);
            await _state.SetDateTimeAsync(SyncStateKeys.FullReloadGoodsStartedAt, DateTime.UtcNow, ct);
            _logger.LogInformation("Full reload инициализирован: {Total} страниц", totalPages);
        }

        // Загружаем ChunkSize страниц
        var pagesLoaded = 0;
        while (pagesLoaded < _options.FullReloadChunkSize && currentPage <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var goods = await LoadGoodsPageAsync(currentPage, ct);
            foreach (var apiGood in goods)
            {
                await UpsertGoodAsync(apiGood, ct);
            }

            _logger.LogDebug("Full reload: страница {Page}/{Total}", currentPage, totalPages);
            
            currentPage++;
            pagesLoaded++;
            await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, currentPage, ct);
        }

        // Проверяем завершение цикла
        if (currentPage > totalPages)
        {
            await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, 1, ct);
            await _state.SetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, 0, ct);
            await _state.SetLastRunAsync(SyncStateKeys.GoodsLastFull, DateTime.UtcNow, ct);
            _logger.LogInformation("Full reload завершён!");
            return false; // Больше нет работы
        }

        return true; // Есть ещё работа
    }

    /// <summary>
    /// Проверить, есть ли незавершённая работа full reload
    /// </summary>
    public async Task<bool> HasPendingFullReloadWorkAsync(CancellationToken ct = default)
    {
        // Проверяем, нужен ли full reload по времени
        var lastFull = await _state.GetLastRunAsync(SyncStateKeys.GoodsLastFull, ct);
        if (lastFull != null && DateTime.UtcNow - lastFull.Value < _options.FullReloadTargetInterval)
        {
            // Full reload ещё не нужен
            var currentPage = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsCurrentPage, 1, ct);
            var totalPages = await _state.GetIntAsync(SyncStateKeys.FullReloadGoodsTotalPages, 0, ct);
            // Но если есть незавершённый — продолжаем
            return totalPages > 0 && currentPage <= totalPages;
        }
        return true; // Нужен full reload
    }

    private async Task<ApiGood[]> LoadGoodsPageAsync(int page, CancellationToken ct)
    {
        // Используем основной метод с пагинацией
        // TODO: добавить поддержку пагинации в API клиент
        var allGoods = await _client.GetGoodsAsync(cancellationToken: ct);
        var skip = (page - 1) * _options.PageSize;
        return allGoods.Skip(skip).Take(_options.PageSize).ToArray();
    }

    private async Task UpsertGoodAsync(ApiGood apiGood, CancellationToken ct)
    {
        var id = int.Parse(apiGood.Id);
        var existing = await _db.Goods.FirstOrDefaultAsync(g => g.Id == id, ct);

        var rawData = JsonSerializer.Serialize(apiGood);
        var price = apiGood.Prices?.FirstOrDefault()?.Price ?? 0;

        if (existing != null)
        {
            await _db.Goods
                .Where(g => g.Id == id)
                .Set(g => g.Name, apiGood.Name ?? "")
                .Set(g => g.Article, apiGood.PartNumber)
                .Set(g => g.Code, apiGood.StoreCode)
                .Set(g => g.IsArchive, apiGood.Archive)
                .Set(g => g.Price, price)
                .Set(g => g.RawData, rawData)
                .Set(g => g.LastSyncedAt, DateTime.UtcNow)
                .Set(g => g.InternalUpdatedAt, DateTime.UtcNow)
                .UpdateAsync(ct);
        }
        else
        {
            await _db.InsertAsync(new Good
            {
                Id = id,
                Name = apiGood.Name ?? "",
                Article = apiGood.PartNumber,
                Code = apiGood.StoreCode,
                IsArchive = apiGood.Archive,
                Price = price,
                RawData = rawData,
                LastSyncedAt = DateTime.UtcNow,
                InternalUpdatedAt = DateTime.UtcNow
            }, token: ct);
        }
    }

    // ========== Public helpers for InitialSyncRunner ==========

    /// <summary>
    /// Получить общее количество товаров
    /// </summary>
    public Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => _client.CountGoodsAsync(cancellationToken: ct);

    /// <summary>
    /// Загрузить и сохранить одну страницу товаров
    /// </summary>
    public async Task LoadAndSavePageAsync(int page, CancellationToken ct = default)
    {
        var goods = await LoadGoodsPageAsync(page, ct);
        foreach (var apiGood in goods)
        {
            await UpsertGoodAsync(apiGood, ct);
        }
    }

    /// <summary>
    /// Получить список ID всех товаров из БД
    /// </summary>
    public async Task<List<int>> GetAllGoodIdsAsync(CancellationToken ct = default)
    {
        return await _db.Goods
            .Select(g => g.Id)
            .ToListAsync(ct);
    }
}
