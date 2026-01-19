using FluentMigrator;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(4, "Add Attributes and AttributeValues tables")]
public class M0004_Attributes : Migration
{
    public override void Up()
    {
        Create.Table("attributes")
            .WithColumn("Id").AsInt64().PrimaryKey() // ID from Business.ru
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("Selectable").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Archive").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Description").AsString(int.MaxValue).Nullable()
            .WithColumn("Sort").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("Deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("attribute_values")
            .WithColumn("Id").AsInt64().PrimaryKey() // ID from Business.ru
            .WithColumn("AttributeId").AsInt64().NotNullable()
                .ForeignKey("FK_attribute_values_attributes", "attributes", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("Sort").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
            
        Create.Index("IX_attribute_values_AttributeId").OnTable("attribute_values").OnColumn("AttributeId");

        Create.Table("good_attributes")
            .WithColumn("Id").AsInt64().PrimaryKey() // ID from Business.ru
            .WithColumn("GoodId").AsInt64().NotNullable()
                .ForeignKey("FK_good_attributes_goods", "goods", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("AttributeId").AsInt64().NotNullable()
                .ForeignKey("FK_good_attributes_attributes", "attributes", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ValueId").AsInt64().Nullable()
                // Relationship to attribute_values is optional/loose in Business.ru? 
                // Docs say "External Key to attributesforgoodsvalues".
                // Adding FK for data integrity if we trust the order of sync.
                .ForeignKey("FK_good_attributes_attribute_values", "attribute_values", "Id").OnDelete(System.Data.Rule.SetNull)
            .WithColumn("Value").AsString(int.MaxValue).Nullable()
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_good_attributes_GoodId").OnTable("good_attributes").OnColumn("GoodId");
        Create.Index("IX_good_attributes_AttributeId").OnTable("good_attributes").OnColumn("AttributeId");
    }

    public override void Down()
    {
        Delete.Table("good_attributes");
        Delete.Table("attribute_values");
        Delete.Table("attributes");
    }
}
