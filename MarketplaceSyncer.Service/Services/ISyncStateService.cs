using ErrorOr;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Сервис управления состоянием синхронизации.
/// Отвечает за создание сессий, отслеживание прогресса и расчет фильтров.
/// </summary>
public interface ISyncStateService
{
    /// <summary>
    /// Находит последнюю завершенную сессию (для расчета инкрементальных фильтров).
    /// </summary>
    Task<SyncSession?> GetLastSuccessfulSessionAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Находит последнюю сессию определенного типа (например, для продолжения Initial).
    /// </summary>
    Task<SyncSession?> GetLastSessionAsync(string entityType, SyncType syncType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Начинает новую сессию синхронизации.
    /// </summary>
    Task<ErrorOr<SyncSession>> StartSessionAsync(
        string entityType, 
        SyncType syncType, 
        DateTimeOffset? filterDateFrom = null, 
        string? cursor = null,
        object? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет прогресс текущей сессии.
    /// </summary>
    Task UpdateProgressAsync(
        long sessionId, 
        int fetched, 
        int processed, 
        string? cursor, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершает сессию успешно или с ошибкой.
    /// </summary>
    Task CompleteSessionAsync(
        long sessionId, 
        SyncStatus status, 
        string? errorDetails = null, 
        CancellationToken cancellationToken = default);
}
