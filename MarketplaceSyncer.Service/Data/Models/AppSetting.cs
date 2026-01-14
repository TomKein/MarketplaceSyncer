using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

[Table("app_settings")]
public class AppSetting
{
    [PrimaryKey, NotNull] public required string Key { get; set; }
    [Column, Nullable] public string? Value { get; set; }
    [Column, NotNull] public DateTime UpdatedAt { get; set; }
}
