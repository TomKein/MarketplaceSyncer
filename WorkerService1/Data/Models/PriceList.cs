using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

/// <summary>
/// Прайс-лист (связывает тип цены с товарами)
/// </summary>
[Table("price_lists")]
public class PriceList
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("business_id"), NotNull]
    public long BusinessId { get; set; }

    [Column("external_id"), NotNull]
    public string ExternalId { get; set; } = string.Empty;

    [Column("price_type_id"), NotNull]
    public long PriceTypeId { get; set; }

    [Column("name"), NotNull]
    public string Name { get; set; } = string.Empty;

    [Column("is_active"), NotNull]
    public bool IsActive { get; set; } = true;

    [Column("created_at"), NotNull]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at"), NotNull]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    [Association(ThisKey = nameof(BusinessId), OtherKey = nameof(Models.Business.Id))]
    public Business? Business { get; set; }

    [Association(ThisKey = nameof(PriceTypeId), OtherKey = nameof(Models.PriceType.Id))]
    public PriceType? PriceType { get; set; }
}
