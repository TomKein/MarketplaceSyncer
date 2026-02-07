using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

/// <summary>
/// Тип цены (Розничная, Оптовая и т.д.)
/// </summary>
[Table("price_types")]
public class PriceType
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("business_id"), NotNull]
    public long BusinessId { get; set; }

    [Column("external_id"), NotNull]
    public string ExternalId { get; set; } = string.Empty;

    [Column("name"), NotNull]
    public string Name { get; set; } = string.Empty;

    [Column("is_sale_price"), NotNull]
    public bool IsSalePrice { get; set; } = false;

    [Column("is_buy_price"), NotNull]
    public bool IsBuyPrice { get; set; } = false;

    [Column("created_at"), NotNull]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at"), NotNull]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation property
    [Association(ThisKey = nameof(BusinessId), OtherKey = nameof(Models.Business.Id))]
    public Business? Business { get; set; }
}
