using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

/// <summary>
/// Товар из Business.ru
/// </summary>
[Table("goods")]
public class Good
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("business_id"), NotNull]
    public long BusinessId { get; set; }

    [Column("external_id"), NotNull]
    public string ExternalId { get; set; } = string.Empty;

    [Column("name"), NotNull]
    public string Name { get; set; } = string.Empty;

    [Column("part_number"), Nullable]
    public string? PartNumber { get; set; }

    [Column("store_code"), Nullable]
    public string? StoreCode { get; set; }

    [Column("archive"), NotNull]
    public bool Archive { get; set; } = false;

    [Column("created_at"), NotNull]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at"), NotNull]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("last_synced_at"), Nullable]
    public DateTimeOffset? LastSyncedAt { get; set; }

    // Navigation property
    [Association(ThisKey = nameof(BusinessId), OtherKey = nameof(Models.Business.Id))]
    public Business? Business { get; set; }
}
