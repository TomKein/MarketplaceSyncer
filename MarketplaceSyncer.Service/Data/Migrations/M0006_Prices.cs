using FluentMigrator;
using System;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(6, "Prices")]
public class M0006_Prices : Migration
{
    public override void Up()
    {
        // 1. Price Types (salepricetypes)
        Create.Table("price_types")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("CurrencyId").AsInt64().Nullable().ForeignKey("currencies", "Id").OnDelete(System.Data.Rule.SetNull)
            .WithColumn("IsArchive").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        // 2. Good Prices (prices linked to goods from goods response)
        // Note: We don't have stable IDs for price entries in goods response, so we use composite PK.
        Create.Table("good_prices")
            .WithColumn("GoodId").AsInt64().NotNullable().ForeignKey("goods", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("PriceTypeId").AsInt64().NotNullable().ForeignKey("price_types", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Price").AsDecimal().NotNullable()
            .WithColumn("Currency").AsString().Nullable() // Store raw currency code from response (e.g. "RUB")
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
            
        Create.PrimaryKey("PK_GoodPrices").OnTable("good_prices").Columns("GoodId", "PriceTypeId");
    }

    public override void Down()
    {
        Delete.Table("good_prices");
        Delete.Table("price_types");
    }
}
