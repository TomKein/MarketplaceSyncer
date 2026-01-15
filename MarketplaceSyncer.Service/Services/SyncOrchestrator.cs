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
    private readonly InitialSyncRunner _initialSync;
    private readonly SyncStateRepository _state;
    private readonly ReferenceSyncer _references;
    private readonly GoodsSyncer _goods;
    private readonly SynchronizationOptions _options;
    private readonly ILogger<SyncOrchestrator> _logger;

    public SyncOrchestrator(
        InitialSyncRunner initialSync,
        SyncStateRepository state,
        ReferenceSyncer references,
        GoodsSyncer goods,
        IOptions<SynchronizationOptions> options,
        ILogger<SyncOrchestrator> logger)
    {
        _initialSync = initialSync;
        _state = state;
        _references = references;
        _goods = goods;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncOrchestrator запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 🔴 HIGH: блокирующая инициальная загрузка
                if (!await _initialSync.IsCompleteAsync(stoppingToken))
                {
                    await _initialSync.RunAsync(stoppingToken);
                    continue;
                }

                // 🟡 MEDIUM: проверяем просроченные incremental задачи
                if (await RunDueIncrementalTasksAsync(stoppingToken))
                {
                    continue; // После инкрементов проверяем снова
                }

                // 🟢 LOW: работаем над full reload в свободное время
                if (await _goods.HasPendingFullReloadWorkAsync(stoppingToken))
                {
                    var hasMore = await _goods.RunFullReloadChunkAsync(stoppingToken);
                    if (hasMore)
                    {
                        continue; // Есть ещё работа — сразу проверяем MEDIUM
                    }
                }

                // Нет работы — ждём до следующего срока MEDIUM
                var waitTime = await CalculateNextWaitTimeAsync(stoppingToken);
                _logger.LogDebug("Ожидание {WaitTime} до следующей задачи", waitTime);
                await Task.Delay(waitTime, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в главном цикле синхронизации");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("SyncOrchestrator остановлен");
    }

    /// <summary>
    /// 🟡 MEDIUM: Выполнить просроченные incremental задачи
    /// </summary>
    private async Task<bool> RunDueIncrementalTasksAsync(CancellationToken ct)
    {
        var anyExecuted = false;

        // Товары delta
        if (await IsGoodsDeltaDueAsync(ct))
        {
            _logger.LogInformation("🟡 Запуск delta sync товаров...");
            await _goods.RunDeltaSyncAsync(ct);
            anyExecuted = true;
        }

        // Справочники (раз в день)
        if (await IsReferencesDueAsync(ct))
        {
            _logger.LogInformation("🟡 Запуск sync справочников...");
            await _references.RunFullSyncAsync(ct);
            await _state.SetLastRunAsync(SyncStateKeys.ReferencesLastRun, DateTime.UtcNow, ct);
            anyExecuted = true;
        }

        // TODO: Images delta
        // if (await IsImagesDeltaDueAsync(ct)) { ... }

        return anyExecuted;
    }

    private async Task<bool> IsGoodsDeltaDueAsync(CancellationToken ct)
    {
        var lastRun = await _state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        if (lastRun == null) return true;
        return DateTime.UtcNow - lastRun.Value >= _options.GoodsDeltaInterval;
    }

    private async Task<bool> IsReferencesDueAsync(CancellationToken ct)
    {
        var lastRun = await _state.GetLastRunAsync(SyncStateKeys.ReferencesLastRun, ct);
        if (lastRun == null) return true;
        return DateTime.UtcNow - lastRun.Value >= _options.ReferencesInterval;
    }

    private async Task<TimeSpan> CalculateNextWaitTimeAsync(CancellationToken ct)
    {
        var waitTimes = new List<TimeSpan>();

        // Goods delta
        var goodsLastRun = await _state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        if (goodsLastRun != null)
        {
            var nextRun = goodsLastRun.Value + _options.GoodsDeltaInterval;
            var remaining = nextRun - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                waitTimes.Add(remaining);
        }

        // References
        var refsLastRun = await _state.GetLastRunAsync(SyncStateKeys.ReferencesLastRun, ct);
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
