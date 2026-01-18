namespace MarketplaceSyncer.Service.Configuration;

public class SynchronizationOptions
{
    /// <summary>
    /// Интервал инкрементальной синхронизации товаров (по полю updated).
    /// </summary>
    public TimeSpan GoodsDeltaInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал инкрементальной синхронизации изображений.
    /// </summary>
    public TimeSpan ImagesDeltaInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал синхронизации справочников (Группы, Единицы измерения).
    /// </summary>
    public TimeSpan ReferencesInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Целевой интервал полной перезагрузки (раз в день).
    /// LOW приоритет работает в фоне для достижения этой цели.
    /// </summary>
    public TimeSpan FullReloadTargetInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Количество страниц за одну итерацию полной загрузки (LOW приоритет).
    /// </summary>
    public int FullReloadChunkSize { get; set; } = 5;

    /// <summary>
    /// Размер страницы при загрузке товаров.
    /// </summary>
    /// <summary>
    /// Размер страницы при загрузке товаров.
    /// </summary>
    public int PageSize { get; set; } = 250;

    /// <summary>
    /// Если true, при старте будет выполнен полный сброс состояния синхронизации и повторная инициальная загрузка.
    /// Осторожно: сбрасывает все курсоры!
    /// </summary>
    public bool ForceInitialResync { get; set; } = false;

    /// <summary>
    /// Время запуска полной ежедневной пересинхронизации (по часовому поясу FullResyncTimeZoneOffset).
    /// По умолчанию: 01:00.
    /// </summary>
    public TimeSpan DailyFullResyncTime { get; set; } = new TimeSpan(1, 0, 0);

    /// <summary>
    /// Смещение часового пояса для ежедневной пересинхронизации (по умолчанию +3 Москва).
    /// </summary>
    public int FullResyncTimeZoneOffset { get; set; } = 3;
}
