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

        // [Startup] Проверка принудительного сброса (Force Initial Resync)
        if (_options.ForceInitialResync)
        {
            _logger.LogWarning("⚠️ ВКЛЮЧЕН FORCE INITIAL RESYNC (как Daily)! Сброс Daily состояния...");
            using (var scope = _scopeFactory.CreateScope())
            {
                var initialSync = scope.ServiceProvider.GetRequiredService<InitialSyncRunner>();
                var state = scope.ServiceProvider.GetRequiredService<SyncStateRepository>();
                
                await initialSync.ResetDailyProgressAsync(stoppingToken);
                // Сбрасываем StartedAt, чтобы цикл сразу подхватил как "нужно начать"
                await state.SetAsync(SyncStateKeys.DailyStartedAt, null, stoppingToken);
            }
        }

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

                    // 1. Проверяем состояние Daily Sync
                    var dailyStartedAt = await state.GetDateTimeOffsetAsync(SyncStateKeys.DailyStartedAt, stoppingToken);
                    var isDailyComplete = await state.GetBoolAsync(SyncStateKeys.DailyComplete, false, stoppingToken);
                    
                    var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(_options.FullResyncTimeZoneOffset));
                    var todayTargetTime = now.Date.Add(_options.DailyFullResyncTime); // Сегодня 1:00
                    if (now < todayTargetTime) todayTargetTime = todayTargetTime.AddDays(-1); // Или вчера 1:00

                    bool startNewDaily = false;
                    bool continueDaily = false;

                    // Если еще не начинали сегодня (или вообще никогда) — пора начинать?
                    if (dailyStartedAt == null || dailyStartedAt.Value < todayTargetTime)
                    {
                        // Пора начинать новый цикл
                        startNewDaily = true;
                    }
                    else if (!isDailyComplete)
                    {
                        // Начали сегодня, но не закончили (падали или просто идем)
                        continueDaily = true;
                    }

                    if (startNewDaily)
                    {
                        _logger.LogInformation("🕒 Наступило время Ежедневного Ресинка. Запуск...");
                        await initialSync.ResetDailyProgressAsync(stoppingToken);
                        await state.SetLastRunAsync(SyncStateKeys.DailyStartedAt, DateTimeOffset.UtcNow, stoppingToken);
                        
                        await initialSync.RunDailyAsync(stoppingToken);
                        waitTime = TimeSpan.Zero;
                    }
                    else if (continueDaily)
                    {
                        _logger.LogInformation("🔄 Продолжение Ежедневного Ресинка...");
                        await initialSync.RunDailyAsync(stoppingToken);
                        waitTime = TimeSpan.Zero;
                    }
                    // 🔴 HIGH: блокирующая инициальная загрузка (только если вообще чистая база)
                    else if (!await initialSync.IsCompleteAsync(stoppingToken))
                    {
                        await initialSync.RunAsync(stoppingToken);
                        waitTime = TimeSpan.Zero; 
                    }

                    // 🟡 MEDIUM: проверяем просроченные incremental задачи
                    else if (await RunDueIncrementalTasksAsync(state, references, goods, stoppingToken))
                    {
                        waitTime = TimeSpan.Zero; 
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

        return anyExecuted;
    }

    private async Task<bool> IsGoodsDeltaDueAsync(SyncStateRepository state, CancellationToken ct)
    {
        var lastRun = await state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        if (lastRun == null) return true;
        return DateTimeOffset.UtcNow - lastRun.Value >= _options.GoodsDeltaInterval;
    }



    private async Task<TimeSpan> CalculateNextWaitTimeAsync(SyncStateRepository state, CancellationToken ct)
    {
        var waitTimes = new List<TimeSpan>();

        // Goods delta
        var goodsLastRun = await state.GetLastRunAsync(SyncStateKeys.GoodsLastDelta, ct);
        if (goodsLastRun != null)
        {
            var nextRun = goodsLastRun.Value + _options.GoodsDeltaInterval;
            var remaining = nextRun - DateTimeOffset.UtcNow;
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
