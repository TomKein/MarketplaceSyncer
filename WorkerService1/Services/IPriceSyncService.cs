namespace WorkerService1.Services;

/// <summary>
/// Интерфейс сервиса синхронизации цен
/// </summary>
public interface IPriceSyncService
{
    /// <summary>
    /// Запустить полный цикл синхронизации цен
    /// </summary>
    Task<bool> ExecutePriceSyncAsync(CancellationToken cancellationToken = default);
}
