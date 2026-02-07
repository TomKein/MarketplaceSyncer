using System;
using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

[Table("price_update_logs")]
public class PriceUpdateLog
{
	[Column("id")]
	[PrimaryKey]
	[Identity]
	public long Id { get; set; }

	[Column("good_price_id")]
	[NotNull]
	public long GoodPriceId { get; set; }

	[Column("old_price")]
	[Nullable]
	public decimal? OldPrice { get; set; }

	[Column("new_price")]
	[Nullable]
	public decimal? NewPrice { get; set; }

	[Column("action_type")]
	[NotNull]
	public string ActionType { get; set; } = string.Empty;

	[Column("error_message")]
	[Nullable]
	public string? ErrorMessage { get; set; }

	[Column("created_at")]
	[NotNull]
	public DateTimeOffset CreatedAt { get; set; }

	[Association(ThisKey = "GoodPriceId", OtherKey = "Id")]
	public GoodPrice? GoodPrice { get; set; }
}
