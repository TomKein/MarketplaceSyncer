namespace MarketplaceSyncer.Service.Configuration;

/// <summary>
/// Настройки синхронизации с Business.ru
/// </summary>
public class SynchronizationOptions
{
    // ========== Инкрементальная синхронизация ==========

    /// <summary>
    /// Интервал инкрементальной синхронизации товаров (по полю updated).
    /// </summary>
    public TimeSpan GoodsDeltaInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал инкрементальной синхронизации цен (currentprices).
    /// </summary>
    public TimeSpan PricesDeltaInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Интервал инкрементальной синхронизации остатков (storegoods).
    /// </summary>
    public TimeSpan StockDeltaInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал инкрементальной синхронизации изображений.
    /// </summary>
    public TimeSpan ImagesDeltaInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал синхронизации справочников (Группы, Единицы измерения).
    /// </summary>
    public TimeSpan ReferencesInterval { get; set; } = TimeSpan.FromHours(24);

    // ========== Полная синхронизация ==========

    /// <summary>
    /// Максимальный возраст последней полной синхронизации.
    /// Если прошло больше времени - запускается новая полная синхронизация.
    /// </summary>
    public TimeSpan FullSyncMaxAge { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Время начала ночного окна полной синхронизации (по FullSyncTimeZoneOffset).
    /// </summary>
    public TimeSpan FullSyncWindowStart { get; set; } = new TimeSpan(1, 0, 0); // 01:00

    /// <summary>
    /// Время окончания ночного окна полной синхронизации (по FullSyncTimeZoneOffset).
    /// После этого времени полная синхронизация прерывается до следующей ночи.
    /// </summary>
    public TimeSpan FullSyncWindowEnd { get; set; } = new TimeSpan(5, 0, 0); // 05:00

    /// <summary>
    /// Смещение часового пояса для ночного окна (по умолчанию +3 Москва).
    /// </summary>
    public int FullSyncTimeZoneOffset { get; set; } = 3;

    /// <summary>
    /// Размер страницы при загрузке товаров.
    /// </summary>
    public int PageSize { get; set; } = 250;

    /// <summary>
    /// Если true, при старте будет выполнен принудительный сброс и запуск полной синхронизации.
    /// </summary>
    public bool ForceFullSync { get; set; } = false;
}
