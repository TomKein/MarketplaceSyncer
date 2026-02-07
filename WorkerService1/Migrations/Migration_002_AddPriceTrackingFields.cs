using FluentMigrator;

namespace WorkerService1.Migrations;

[Migration(2)]
public class Migration_002_AddPriceTrackingFields : Migration
{
    public override void Up()
    {
        // Add new fields to good_prices table for better price tracking
        Alter.Table("good_prices")
            .AddColumn("price_type_id").AsString(50).NotNullable()
                .WithDefaultValue(string.Empty)
            .AddColumn("price_list_good_id").AsString(50).NotNullable()
                .WithDefaultValue(string.Empty)
            .AddColumn("businessru_updated_at").AsDateTimeOffset().Nullable();

        // Add index for faster queries by price_type_id
        Create.Index("idx_good_prices_price_type_id")
            .OnTable("good_prices")
            .OnColumn("price_type_id");

        // Add index for price_list_good_id lookups
        Create.Index("idx_good_prices_price_list_good_id")
            .OnTable("good_prices")
            .OnColumn("price_list_good_id");

        // Add composite index for common query pattern
        Create.Index("idx_good_prices_good_price_type")
            .OnTable("good_prices")
            .OnColumn("good_id").Ascending()
            .OnColumn("price_type_id").Ascending();
    }

    public override void Down()
    {
        // Remove indexes
        Delete.Index("idx_good_prices_price_type_id")
            .OnTable("good_prices");
        
        Delete.Index("idx_good_prices_price_list_good_id")
            .OnTable("good_prices");
        
        Delete.Index("idx_good_prices_good_price_type")
            .OnTable("good_prices");

        // Remove columns
        Delete.Column("price_type_id").FromTable("good_prices");
        Delete.Column("price_list_good_id").FromTable("good_prices");
        Delete.Column("businessru_updated_at").FromTable("good_prices");
    }
}
