using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

public enum SyncType
{
    [MapValue("FULL")] Full,
    [MapValue("INCREMENTAL")] Incremental,
    [MapValue("INITIAL")] Initial
}

public enum SyncStatus
{
    [MapValue("IN_PROGRESS")] InProgress,
    [MapValue("COMPLETED")] Completed,
    [MapValue("FAILED")] Failed
}

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
    public SyncType SyncType { get; set; } = SyncType.Full;

    [Column("StartedAt"), NotNull]
    public DateTimeOffset StartedAt { get; set; }

    [Column("CompletedAt"), Nullable]
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// IN_PROGRESS, COMPLETED, FAILED
    /// </summary>
    [Column("Status"), NotNull]
    public SyncStatus Status { get; set; } = SyncStatus.InProgress;

    [Column("ItemsFetched"), NotNull]
    public int ItemsFetched { get; set; }

    [Column("ItemsProcessed"), NotNull]
    public int ItemsProcessed { get; set; }

    [Column("ErrorsCount"), NotNull]
    public int ErrorsCount { get; set; }

    [Column("ErrorDetails"), Nullable]
    public string? ErrorDetails { get; set; }

    [Column("EntityType"), NotNull]
    public string EntityType { get; set; } = "Unknown";

    [Column("FilterDateFrom"), Nullable]
    public DateTimeOffset? FilterDateFrom { get; set; }

    [Column("Cursor"), Nullable]
    public string? Cursor { get; set; }

    [Column(DataType = LinqToDB.DataType.BinaryJson), Nullable] 
    public string? Config { get; set; }
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
