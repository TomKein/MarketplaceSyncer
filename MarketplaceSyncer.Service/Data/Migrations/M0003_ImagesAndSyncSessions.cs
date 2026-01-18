using FluentMigrator;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(3, "Add GoodImages and SyncSessions tables")]
public class M0003_ImagesAndSyncSessions : Migration
{
    public override void Up()
    {
        // Таблица изображений товаров
        Create.Table("good_images")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("GoodId").AsInt64().NotNullable()
                .ForeignKey("FK_good_images_goods", "goods", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Name").AsString(255).Nullable()
            .WithColumn("Url").AsString(1000).NotNullable()
            .WithColumn("Data").AsBinary(int.MaxValue).Nullable()
            .WithColumn("ContentType").AsString(50).Nullable()
            .WithColumn("Hash").AsString(64).NotNullable()
            .WithColumn("Position").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DownloadedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_good_images_GoodId").OnTable("good_images").OnColumn("GoodId");
        Create.Index("IX_good_images_Hash").OnTable("good_images").OnColumn("Hash");

        // Таблица сессий синхронизации
        Create.Table("sync_sessions")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("SyncType").AsString(50).NotNullable() // FULL, INCREMENTAL
            .WithColumn("StartedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("CompletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("Status").AsString(50).NotNullable().WithDefaultValue("IN_PROGRESS") // IN_PROGRESS, COMPLETED, FAILED
            .WithColumn("ItemsFetched").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ItemsProcessed").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ErrorsCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ErrorDetails").AsCustom("JSONB").Nullable()
            
            // New columns for Sync State Strategy
            .WithColumn("EntityType").AsString(50).NotNullable().WithDefaultValue("Unknown") 
            .WithColumn("FilterDateFrom").AsDateTimeOffset().Nullable()
            .WithColumn("Cursor").AsString(100).Nullable()
            .WithColumn("Config").AsCustom("JSONB").Nullable();

        Create.Index("IX_sync_sessions_StartedAt").OnTable("sync_sessions").OnColumn("StartedAt").Descending();

        // Таблица событий синхронизации
        Create.Table("sync_events")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("EventType").AsString(100).NotNullable() // goods.updated, stock.changed, price.changed
            .WithColumn("EntityType").AsString(50).NotNullable() // good, price, stock, image
            .WithColumn("EntityId").AsString(200).NotNullable()
            .WithColumn("Payload").AsCustom("JSONB").Nullable()
            .WithColumn("Status").AsString(50).NotNullable().WithDefaultValue("PENDING") // PENDING, PROCESSED, FAULTED
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("ProcessedAt").AsDateTimeOffset().Nullable();

        Create.Index("IX_sync_events_Status").OnTable("sync_events").OnColumn("Status");
        Create.Index("IX_sync_events_CreatedAt").OnTable("sync_events").OnColumn("CreatedAt").Descending();
    }

    public override void Down()
    {
        Delete.Table("sync_events");
        Delete.Table("sync_sessions");
        Delete.Table("good_images");
    }
}
