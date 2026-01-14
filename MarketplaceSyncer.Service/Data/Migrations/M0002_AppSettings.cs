using FluentMigrator;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(2, "Add AppSettings table for sync state")]
public class M0002_AppSettings : Migration
{
    public override void Up()
    {
        Create.Table("app_settings")
            .WithColumn("Key").AsString().PrimaryKey()
            .WithColumn("Value").AsString().Nullable()
            .WithColumn("UpdatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
            
        // Insert initial keys if needed or leave empty
        // Insert.IntoTable("app_settings").Row(new { Key = "Groups_LastSync", Value = "", UpdatedAt = DateTime.UtcNow });
    }

    public override void Down()
    {
        Delete.Table("app_settings");
    }
}
