using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Configuration;
using Microsoft.Extensions.Options;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Главный оркестратор синхронизации.
/// Управляет полной и инкрементальной синхронизацией с учётом ночного окна.
/// </summary>
public class SyncOrchestrator(
    IServiceScopeFactory scopeFactory,
    IOptions<SynchronizationOptions> options,
    ILogger<SyncOrchestrator> logger)
    : BackgroundService
{
    private readonly SynchronizationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SyncOrchestrator запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan waitTime;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var state = scope.ServiceProvider.GetRequiredService<SyncStateRepository>();
                var fullSync = CreateFullSyncRunner(scope.ServiceProvider);
                var goods = scope.ServiceProvider.GetRequiredService<GoodsSyncer>();
                var prices = scope.ServiceProvider.GetRequiredService<PriceSyncer>();
                var stock = scope.ServiceProvider.GetRequiredService<StockSyncer>();

                var now = GetLocalTime();
                var isNightWindow = IsInNightWindow(now);

                // 1. Проверка принудительного сброса
                if (_options.ForceFullSync)
                {
                    logger.LogWarning("FORCE FULL SYNC включен! Сброс и запуск полной синхронизации...");
                    await fullSync.ResetProgressAsync(stoppingToken);
                    await fullSync.RunAsync(() => false, stoppingToken);
                    waitTime = TimeSpan.Zero;
                    continue;
                }

                // 2. Незавершенная полная синхронизация?
                if (await fullSync.IsInProgressAsync(stoppingToken))
                {
                    if (isNightWindow)
                    {
                        logger.LogInformation("Продолжение незавершенной полной синхронизации (ночное окно)...");
                        var completed = await fullSync.RunAsync(() => !IsInNightWindow(GetLocalTime()), stoppingToken);

                        if (!completed)
                            logger.LogInformation("Полная синхронизация прервана (конец ночного окна). Продолжим следующей ночью.");

                        waitTime = TimeSpan.Zero;
                        continue;
                    }

                    // Вне ночного окна - логируем и переходим к инкрементальной
                    logger.LogDebug("Есть незавершенная полная синхронизация, но сейчас не ночное окно. Переход к инкрементальной.");
                }

                // 3. Нужна полная синхронизация? (> FullSyncMaxAge)
                if (await fullSync.IsFullSyncNeededAsync(stoppingToken))
                {
                    if (isNightWindow)
                    {
                        logger.LogInformation("Запуск полной синхронизации (прошло > {MaxAge})...", _options.FullSyncMaxAge);
                        await fullSync.ResetProgressAsync(stoppingToken);
                        var completed = await fullSync.RunAsync(() => !IsInNightWindow(GetLocalTime()), stoppingToken);

                        if (!completed)
                            logger.LogInformation("Полная синхронизация прервана (конец ночного окна). Продолжим следующей ночью.");

                        waitTime = TimeSpan.Zero;
                        continue;
                    }

                    // Первый запуск - запускаем сразу, даже если не ночь
                    var lastCompleted = await fullSync.GetLastCompletedAtAsync(stoppingToken);
                    if (lastCompleted == null)
                    {
                        logger.LogInformation("Первый запуск - запуск полной синхронизации...");
                        await fullSync.ResetProgressAsync(stoppingToken);
                        await fullSync.RunAsync(() => false, stoppingToken);
                        waitTime = TimeSpan.Zero;
                        continue;
                    }
                }

                // 4. Инкрементальная синхронизация
                if (await RunIncrementalSyncAsync(state, goods, prices, stock, stoppingToken))
                {
                    waitTime = TimeSpan.Zero;
                    continue;
                }

                // 5. Ничего не делаем - ждём
                waitTime = await CalculateNextWaitTimeAsync(state, stoppingToken);
                logger.LogDebug("Ожидание {WaitTime} до следующей задачи", waitTime);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в главном цикле синхронизации");
                waitTime = TimeSpan.FromSeconds(30);
            }

            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, stoppingToken);
            }
        }

        logger.LogInformation("SyncOrchestrator остановлен");
    }

    /// <summary>
    /// Создать FullSyncRunner с зависимостями из скоупа
    /// </summary>
    private FullSyncRunner CreateFullSyncRunner(IServiceProvider sp)
    {
        return new FullSyncRunner(
            sp.GetRequiredService<SyncStateRepository>(),
            sp.GetRequiredService<ReferenceSyncer>(),
            sp.GetRequiredService<AttributeSyncer>(),
            sp.GetRequiredService<GoodsSyncer>(),
            Options.Create(_options),
            sp.GetRequiredService<ILogger<FullSyncRunner>>());
    }

    /// <summary>
    /// Получить текущее локальное время (по настроенному часовому поясу)
    /// </summary>
    private TimeOnly GetLocalTime()
    {
        var offset = TimeSpan.FromHours(_options.FullSyncTimeZoneOffset);
        var localNow = DateTimeOffset.UtcNow.ToOffset(offset);
        return TimeOnly.FromTimeSpan(localNow.TimeOfDay);
    }

    /// <summary>
    /// Проверить, находимся ли в ночном окне полной синхронизации
    /// </summary>
    private bool IsInNightWindow(TimeOnly currentTime)
    {
        var start = TimeOnly.FromTimeSpan(_options.FullSyncWindowStart);
        var end = TimeOnly.FromTimeSpan(_options.FullSyncWindowEnd);

        // Обработка перехода через полночь (например, 23:00 - 05:00)
        if (start > end)
        {
            return currentTime >= start || currentTime < end;
        }

        return currentTime >= start && currentTime < end;
    }

    /// <summary>
    /// Выполнить просроченные инкрементальные задачи
    /// </summary>
    private async Task<bool> RunIncrementalSyncAsync(
        SyncStateRepository state,
        GoodsSyncer goods,
        PriceSyncer prices,
        StockSyncer stock,
        CancellationToken ct)
    {
        var anyRun = false;

        // Товары
        if (await ShouldRunAsync(state, SyncStateKeys.GoodsLastDelta, _options.GoodsDeltaInterval, ct))
        {
            logger.LogInformation("Запуск инкрементальной синхронизации товаров...");
            await goods.RunDeltaSyncAsync(ct);
            anyRun = true;
        }

        // Цены
        if (await ShouldRunAsync(state, SyncStateKeys.PricesLastDelta, _options.PricesDeltaInterval, ct))
        {
            logger.LogInformation("Запуск инкрементальной синхронизации цен...");
            await prices.RunDeltaSyncAsync(ct);
            anyRun = true;
        }

        // Остатки
        if (await ShouldRunAsync(state, SyncStateKeys.StockLastDelta, _options.StockDeltaInterval, ct))
        {
            logger.LogInformation("Запуск инкрементальной синхронизации остатков...");
            await stock.RunDeltaSyncAsync(ct);
            anyRun = true;
        }

        return anyRun;
    }

    /// <summary>
    /// Проверить, пора ли запускать задачу
    /// </summary>
    private async Task<bool> ShouldRunAsync(
        SyncStateRepository state,
        string stateKey,
        TimeSpan interval,
        CancellationToken ct)
    {
        var lastRun = await state.GetLastRunAsync(stateKey, ct);
        return lastRun == null || DateTimeOffset.UtcNow - lastRun.Value >= interval;
    }

    /// <summary>
    /// Вычислить время до следующей задачи
    /// </summary>
    private async Task<TimeSpan> CalculateNextWaitTimeAsync(SyncStateRepository state, CancellationToken ct)
    {
        var waitTimes = new List<TimeSpan>();

        await AddWaitTimeIfNeededAsync(state, SyncStateKeys.GoodsLastDelta, _options.GoodsDeltaInterval, waitTimes, ct);
        await AddWaitTimeIfNeededAsync(state, SyncStateKeys.PricesLastDelta, _options.PricesDeltaInterval, waitTimes, ct);
        await AddWaitTimeIfNeededAsync(state, SyncStateKeys.StockLastDelta, _options.StockDeltaInterval, waitTimes, ct);

        if (waitTimes.Count == 0)
            return TimeSpan.FromSeconds(10);

        var minWait = waitTimes.Min();
        return minWait > TimeSpan.FromSeconds(5) ? minWait : TimeSpan.FromSeconds(5);
    }

    private async Task AddWaitTimeIfNeededAsync(
        SyncStateRepository state,
        string stateKey,
        TimeSpan interval,
        List<TimeSpan> waitTimes,
        CancellationToken ct)
    {
        var lastRun = await state.GetLastRunAsync(stateKey, ct);
        if (lastRun != null)
        {
            var nextRun = lastRun.Value + interval;
            var remaining = nextRun - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
                waitTimes.Add(remaining);
        }
    }
}
