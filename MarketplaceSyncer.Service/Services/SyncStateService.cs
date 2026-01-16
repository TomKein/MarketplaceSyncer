using System.Text.Json;
using ErrorOr;
using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

public class SyncStateService(AppDataConnection db) : ISyncStateService
{
    public async Task<SyncSession?> GetLastSuccessfulSessionAsync(string entityType, CancellationToken cancellationToken = default)
    {
        return await db.SyncSessions
            .Where(s => s.EntityType == entityType && s.Status == SyncStatus.Completed)
            .OrderByDescending(s => s.CompletedAt) // Берем самую свежую завершенную
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SyncSession?> GetLastSessionAsync(string entityType, SyncType syncType, CancellationToken cancellationToken = default)
    {
        return await db.SyncSessions
            .Where(s => s.EntityType == entityType && s.SyncType == syncType)
            .OrderByDescending(s => s.StartedAt) // Берем самую последнюю попытку
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ErrorOr<SyncSession>> StartSessionAsync(
        string entityType, 
        SyncType syncType, 
        DateTimeOffset? filterDateFrom = null, 
        string? cursor = null,
        object? config = null,
        CancellationToken cancellationToken = default)
    {
        var session = new SyncSession
        {
            EntityType = entityType,
            SyncType = syncType,
            Status = SyncStatus.InProgress,
            StartedAt = DateTimeOffset.UtcNow,
            FilterDateFrom = filterDateFrom,
            Cursor = cursor,
            Config = config != null ? JsonSerializer.Serialize(config) : null,
            ItemsFetched = 0,
            ItemsProcessed = 0,
            ErrorsCount = 0
        };

        var id = await db.InsertWithInt64IdentityAsync(session, token: cancellationToken);
        session.Id = id;

        return session;
    }

    public async Task UpdateProgressAsync(
        long sessionId, 
        int fetched, 
        int processed, 
        string? cursor, 
        CancellationToken cancellationToken = default)
    {
        await db.SyncSessions
            .Where(s => s.Id == sessionId)
            .Set(s => s.ItemsFetched, s => s.ItemsFetched + fetched)
            .Set(s => s.ItemsProcessed, s => s.ItemsProcessed + processed)
            .Set(s => s.Cursor, cursor) // Обновляем курсор на последний успешный
            .UpdateAsync(cancellationToken);
    }

    public async Task CompleteSessionAsync(
        long sessionId, 
        SyncStatus status, 
        string? errorDetails = null, 
        CancellationToken cancellationToken = default)
    {
        await db.SyncSessions
            .Where(s => s.Id == sessionId)
            .Set(s => s.Status, status)
            .Set(s => s.CompletedAt, DateTimeOffset.UtcNow)
            .Set(s => s.ErrorDetails, errorDetails)
            .UpdateAsync(cancellationToken);
    }
}
