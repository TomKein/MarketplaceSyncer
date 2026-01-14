namespace MarketplaceSyncer.Service.Configuration;

public class SynchronizationOptions
{
    /// <summary>
    /// Интервал частичной синхронизации товаров (по полю updated).
    /// </summary>
    public TimeSpan GoodsDeltaSyncInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал полной синхронизации товаров (проход по всем).
    /// </summary>
    public TimeSpan GoodsFullSyncInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Интервал синхронизации справочников (Группы, Единицы измерения и пр.).
    /// </summary>
    public TimeSpan ReferencesSyncInterval { get; set; } = TimeSpan.FromHours(24);
}
