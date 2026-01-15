using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

/// <summary>
/// Хранилище состояния синхронизации (ключ-значение)
/// </summary>
[Table("sync_state")]
public class SyncState
{
    [PrimaryKey, NotNull] public required string Key { get; set; }
    [Column, Nullable] public string? Value { get; set; }
    [Column, NotNull] public DateTime UpdatedAt { get; set; }
}
