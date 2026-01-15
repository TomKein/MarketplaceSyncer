namespace MarketplaceSyncer.Service.Services.Tasks;

/// <summary>
/// Интерфейс задачи синхронизации (MEDIUM приоритет)
/// </summary>
public interface ISyncTask
{
    /// <summary>
    /// Уникальное имя задачи
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Ключ в app_settings для хранения времени последнего запуска
    /// </summary>
    string LastRunKey { get; }
    
    /// <summary>
    /// Интервал между запусками
    /// </summary>
    TimeSpan Interval { get; }
    
    /// <summary>
    /// Выполнить задачу
    /// </summary>
    Task ExecuteAsync(CancellationToken ct = default);
}

/// <summary>
/// Базовый класс для задач синхронизации
/// </summary>
public abstract class SyncTaskBase : ISyncTask
{
    protected readonly SyncStateRepository State;
    protected readonly ILogger Logger;

    protected SyncTaskBase(SyncStateRepository state, ILogger logger)
    {
        State = state;
        Logger = logger;
    }

    public abstract string Name { get; }
    public abstract string LastRunKey { get; }
    public abstract TimeSpan Interval { get; }

    /// <summary>
    /// Проверить, пора ли выполнять задачу
    /// </summary>
    public async Task<bool> IsDueAsync(CancellationToken ct = default)
    {
        var lastRun = await State.GetLastRunAsync(LastRunKey, ct);
        if (lastRun == null) return true; // Никогда не запускалась
        return DateTime.UtcNow - lastRun.Value >= Interval;
    }

    /// <summary>
    /// Время до следующего запуска
    /// </summary>
    public async Task<TimeSpan> GetTimeUntilDueAsync(CancellationToken ct = default)
    {
        var lastRun = await State.GetLastRunAsync(LastRunKey, ct);
        if (lastRun == null) return TimeSpan.Zero;
        var nextRun = lastRun.Value + Interval;
        var remaining = nextRun - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public abstract Task ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Отметить выполнение
    /// </summary>
    protected Task MarkExecutedAsync(CancellationToken ct = default)
        => State.SetLastRunAsync(LastRunKey, DateTime.UtcNow, ct);
}
