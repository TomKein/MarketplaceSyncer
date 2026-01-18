using FluentMigrator;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(2, "Add sync_state table for tracking synchronization progress")]
public class M0002_SyncState : Migration
{
    public override void Up()
    {
        Create.Table("sync_state")
            .WithColumn("Key").AsString().PrimaryKey()
            .WithColumn("Value").AsString().Nullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
    }

    public override void Down()
    {
        Delete.Table("sync_state");
    }
}
