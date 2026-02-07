namespace WorkerService1.Configuration;

/// <summary>
/// Настройки синхронизации цен
/// </summary>
public class PriceSyncOptions
{
    public const string SectionName = "PriceSync";

    /// <summary>
    /// Наценка по умолчанию для расчета цен (15 = 15%)
    /// </summary>
    public decimal DefaultMarkupPercent { get; set; } = 15.0m;

    /// <summary>
    /// Размер батча для обработки цен
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Защита от повторного запуска (дней)
    /// </summary>
    public int ProtectFromDuplicateRunDays { get; set; } = 7;

    /// <summary>
    /// Допустимая погрешность при сравнении цен (0.01 = 1 копейка)
    /// </summary>
    public decimal PriceComparisonTolerance { get; set; } = 0.01m;

    /// <summary>
    /// Шаг округления цен (50 = округление до 50 рублей)
    /// </summary>
    public decimal PriceRoundingStep { get; set; } = 50m;
}