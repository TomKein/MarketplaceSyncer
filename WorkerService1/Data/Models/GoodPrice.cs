using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

/// <summary>
/// Цена товара - КЛЮЧЕВАЯ ТАБЛИЦА
/// Хранит исходную, вычисленную и текущую цены для защиты от повторного повышения
/// </summary>
[Table("good_prices")]
public class GoodPrice
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("business_id"), NotNull]
    public long BusinessId { get; set; }

    [Column("good_id"), NotNull]
    public long GoodId { get; set; }

    [Column("price_list_id"), NotNull]
    public long PriceListId { get; set; }

    [Column("external_price_record_id"), NotNull]
    public string ExternalPriceRecordId { get; set; } = string.Empty;

    /// <summary>
    /// ID типа цены в Business.ru (например, 75524 для розничной цены)
    /// </summary>
    [Column("price_type_id"), NotNull]
    public string PriceTypeId { get; set; } = string.Empty;

    /// <summary>
    /// ID связки товара с прайс-листом (sale_price_list_good_id)
    /// Нужен для корректной работы с API Business.ru
    /// </summary>
    [Column("price_list_good_id"), NotNull]
    public string PriceListGoodId { get; set; } = string.Empty;

    /// <summary>
    /// Дата последнего обновления цены в Business.ru
    /// Используется для определения актуальной версии цены
    /// </summary>
    [Column("businessru_updated_at"), Nullable]
    public DateTimeOffset? BusinessRuUpdatedAt { get; set; }

    /// <summary>
    /// Исходная цена из Business.ru при первой загрузке
    /// </summary>
    [Column("original_price"), NotNull]
    public decimal OriginalPrice { get; set; }

    /// <summary>
    /// Вычисленная цена с наценкой (+15%)
    /// </summary>
    [Column("calculated_price"), Nullable]
    public decimal? CalculatedPrice { get; set; }

    /// <summary>
    /// Текущая цена в Business.ru (обновляется при синхронизации)
    /// </summary>
    [Column("current_price"), NotNull]
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Процент повышения цены
    /// </summary>
    [Column("price_increase_percent"), Nullable]
    public decimal? PriceIncreasePercent { get; set; }

    /// <summary>
    /// Дата расчета новой цены
    /// </summary>
    [Column("calculation_date"), Nullable]
    public DateTimeOffset? CalculationDate { get; set; }

    /// <summary>
    /// Дата обновления цены в Business.ru
    /// </summary>
    [Column("updated_in_businessru_at"), Nullable]
    public DateTimeOffset? UpdatedInBusinessRuAt { get; set; }

    /// <summary>
    /// Флаг обработки - защита от повторного повышения
    /// </summary>
    [Column("is_processed"), NotNull]
    public bool IsProcessed { get; set; } = false;

    [Column("currency_id"), Nullable]
    public string? CurrencyId { get; set; }

    [Column("created_at"), NotNull]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at"), NotNull]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    [Association(ThisKey = nameof(BusinessId), OtherKey = nameof(Models.Business.Id))]
    public Business? Business { get; set; }

    [Association(ThisKey = nameof(GoodId), OtherKey = nameof(Models.Good.Id))]
    public Good? Good { get; set; }

    [Association(ThisKey = nameof(PriceListId), OtherKey = nameof(Models.PriceList.Id))]
    public PriceList? PriceList { get; set; }
}
