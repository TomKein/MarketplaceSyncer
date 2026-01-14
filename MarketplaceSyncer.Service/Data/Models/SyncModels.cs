using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

/// <summary>
/// Tracks synchronization sessions
/// </summary>
[Table("sync_sessions")]
public class SyncSession
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public long Id { get; set; }

    /// <summary>
    /// FULL or INCREMENTAL
    /// </summary>
    [Column("SyncType"), NotNull]
    public string SyncType { get; set; } = "FULL";

    [Column("StartedAt"), NotNull]
    public DateTime StartedAt { get; set; }

    [Column("CompletedAt"), Nullable]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// IN_PROGRESS, COMPLETED, FAILED
    /// </summary>
    [Column("Status"), NotNull]
    public string Status { get; set; } = "IN_PROGRESS";

    [Column("GoodsFetched"), NotNull]
    public int GoodsFetched { get; set; }

    [Column("GoodsSynced"), NotNull]
    public int GoodsSynced { get; set; }

    [Column("ErrorsCount"), NotNull]
    public int ErrorsCount { get; set; }

    [Column("ErrorDetails"), Nullable]
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Individual sync events for processing
/// </summary>
[Table("sync_events")]
public class SyncEvent
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public long Id { get; set; }

    /// <summary>
    /// goods.updated, stock.changed, price.changed
    /// </summary>
    [Column("EventType"), NotNull]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// good, price, stock, image
    /// </summary>
    [Column("EntityType"), NotNull]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntityId"), NotNull]
    public string EntityId { get; set; } = string.Empty;

    [Column("Payload"), Nullable]
    public string? Payload { get; set; }

    /// <summary>
    /// PENDING, PROCESSED, FAULTED
    /// </summary>
    [Column("Status"), NotNull]
    public string Status { get; set; } = "PENDING";

    [Column("CreatedAt"), NotNull]
    public DateTime CreatedAt { get; set; }

    [Column("ProcessedAt"), Nullable]
    public DateTime? ProcessedAt { get; set; }
}
