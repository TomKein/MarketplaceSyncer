using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

/// <summary>
/// Сессия синхронизации цен
/// </summary>
[Table("sync_sessions")]
public class SyncSession
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("business_id"), NotNull]
    public long BusinessId { get; set; }

    [Column("started_at"), NotNull]
    public DateTimeOffset StartedAt { get; set; }

    [Column("completed_at"), Nullable]
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Статус сессии: IN_PROGRESS, COMPLETED, FAILED
    /// </summary>
    [Column("status"), NotNull]
    public string Status { get; set; } = SyncSessionStatus.InProgress;

    [Column("goods_fetched"), NotNull]
    public int GoodsFetched { get; set; } = 0;

    [Column("prices_fetched"), NotNull]
    public int PricesFetched { get; set; } = 0;

    [Column("prices_calculated"), NotNull]
    public int PricesCalculated { get; set; } = 0;

    [Column("prices_updated"), NotNull]
    public int PricesUpdated { get; set; } = 0;

    [Column("errors_count"), NotNull]
    public int ErrorsCount { get; set; } = 0;

    [Column("error_details"), Nullable]
    public string? ErrorDetails { get; set; }

    // Navigation property
    [Association(ThisKey = nameof(BusinessId), OtherKey = nameof(Models.Business.Id))]
    public Business? Business { get; set; }
}

/// <summary>
/// Статусы сессии синхронизации
/// </summary>
public static class SyncSessionStatus
{
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}
