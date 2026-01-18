using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

/// <summary>
/// Хранилище состояния процессов синхронизации (персистентный Key-Value Store).
/// <para>
/// Используется для отслеживания прогресса и точек остановки (checkpoints) различных процессов синхронизации.
/// Это позволяет возобновлять работу после перезапуска или сбоя без потери данных и без необходимости полной пересинхронизации.
/// </para>
/// <para>
/// <b>Key</b>: Уникальный строковый идентификатор параметра (например, "Goods_LastDelta", "Initial_Groups_Complete").<br/>
/// <b>Value</b>: Сериализованное значение состояния (дата, номер страницы, JSON-конфиг и т.д.).
/// </para>
/// </summary>
[Table("sync_state")]
public class SyncState
{
    [PrimaryKey, NotNull] public required string Key { get; set; }
    [Column, Nullable] public string? Value { get; set; }
    [Column, NotNull] public DateTimeOffset UpdatedAt { get; set; }
}
