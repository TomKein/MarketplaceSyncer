using MarketplaceSyncer.Service.Configuration;
using Microsoft.Extensions.Options;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Главный оркестратор синхронизации с приоритетной моделью:
/// 🔴 HIGH: Initial (блокирующий)
/// 🟡 MEDIUM: Incremental (по интервалам)
/// 🟢 LOW: Full Reload (ленивый, в промежутках)
/// </summary>
public class SyncOrchestrator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<SyncOrchestrator> _logger;

    public SyncOrchestrator(
        IServiceScopeFactory scopeFactory,
        IOptions<SynchronizationOptions> options,
        ILogger<SyncOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncOrchestrator запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan? waitTime = null;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var initialSync = scope.ServiceProvider.GetRequiredService<InitialSyncRunner>();
                    var state = scope.ServiceProvider.GetRequiredService<SyncStateRepository>();
                    var references = scope.ServiceProvider.GetRequiredService<ReferenceSyncer>();
                    var goods = scope.ServiceProvider.GetRequiredService<GoodsSyncer>();

                    // 🔴 HIGH: блокирующая инициальная загрузка
                    if (!await initialSync.IsCompleteAsync(stoppingToken))
                    {
                        await initialSync.RunAsync(stoppingToken);
                        waitTime = TimeSpan.Zero; // Сразу проверяем дальше
                    }
                    // 🟡 MEDIUM: проверяем просроченные incremental задачи
                    else if (await RunDueIncrementalTasksAsync(state, references, goods, stoppingToken))
                    {
                        waitTime = TimeSpan.Zero; // Сразу проверяем дальше
                    }
                    // 🟢 LOW: работаем над full reload в свободное время
                    else if (await goods.HasPendingFullReloadWorkAsync(stoppingToken))
                    {
                        var hasMore = await goods.RunFullReloadChunkAsync(stoppingToken);
                        if (hasMore)
                        {
                            waitTime = TimeSpan.Zero; // Есть ещё работа — сразу идем на новый круг
                        }
                    }

                    // Если работа не была выполнена (или закончилась chunk-ом), вычисляем время ожидания
                    if (waitTime == null)
                    {
                        waitTime = await CalculateNextWaitTimeAsync(state, stoppingToken);
                        _logger.LogDebug("Ожидание {WaitTime} до следующей задачи", waitTime);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в главном цикле синхронизации");
                waitTime = TimeSpan.FromSeconds(30);
            }

            // Ждем ВНЕ скоупа, чтобы не держать connection к БД
            if (waitTime.HasValue && waitTime.Value > TimeSpan.Zero)
            {
                await Task.Delay(waitTime.Value, stoppingToken);
            }
        }

        _logger.LogInformation("SyncOrchestrator остановлен");
    }

    /// <summary>
    /// 🟡 MEDIUM: Выполнить просроченные incremental задачи
    /// </summary>
    private async Task<bool> RunDueIncrementalTasksAsync(
        SyncStateRepository state,
        ReferenceSyncer references,
        GoodsSyncer goods,
        CancellationToken ct)
    {
        var anyExecuted = false;

        // Товары delta
        if (await IsGoodsDeltaDueAsync(state, ct))
        {
            _logger.LogInformation("🟡 Запуск delta sync товаров...");
            await goods.RunDeltaSyncAsync(ct);
            anyExecuted = true;
        }

        // Справочники (раз в день)
        if (await IsReferencesDueAsync(state, ct))
        {
            _logger.LogInformation("🟡 Запуск sync справочников...");
            await references.RunFullSyncAsync(ct);
            await state.SetLastRunAsync(SyncStateKeys.ReferencesLastRun, DateTime.UtcNow, ct);
            anyExecuted = true;
        }

        return anyExecuted;
    }

    private async Task<bool> IsGoodsDeltaDueAsync(SyncStateRepository state, CancellationToken ct)
    {
        var lastRun = await state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        if (lastRun == null) return true;
        return DateTime.UtcNow - lastRun.Value >= _options.GoodsDeltaInterval;
    }

    private async Task<bool> IsReferencesDueAsync(SyncStateRepository state, CancellationToken ct)
    {
        var lastRun = await state.GetLastRunAsync(SyncStateKeys.ReferencesLastRun, ct);
        if (lastRun == null) return true;
        return DateTime.UtcNow - lastRun.Value >= _options.ReferencesInterval;
    }

    private async Task<TimeSpan> CalculateNextWaitTimeAsync(SyncStateRepository state, CancellationToken ct)
    {
        var waitTimes = new List<TimeSpan>();

        // Goods delta
        var goodsLastRun = await state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        if (goodsLastRun != null)
        {
            var nextRun = goodsLastRun.Value + _options.GoodsDeltaInterval;
            var remaining = nextRun - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                waitTimes.Add(remaining);
        }

        // References
        var refsLastRun = await state.GetLastRunAsync(SyncStateKeys.ReferencesLastRun, ct);
        if (refsLastRun != null)
        {
            var nextRun = refsLastRun.Value + _options.ReferencesInterval;
            var remaining = nextRun - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                waitTimes.Add(remaining);
        }

        // Минимальное время ожидания
        if (waitTimes.Count == 0)
            return TimeSpan.FromSeconds(10);

        var minWait = waitTimes.Min();
        return minWait > TimeSpan.FromSeconds(5) ? minWait : TimeSpan.FromSeconds(5);
    }
}
