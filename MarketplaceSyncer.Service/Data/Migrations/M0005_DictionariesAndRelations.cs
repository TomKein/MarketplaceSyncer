using FluentMigrator;
using System;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(5, "Dictionaries and Relations")]
public class M0005_DictionariesAndRelations : Migration
{
    public override void Up()
    {
        // 1. Migrate Units -> Measures
        // Remove FK from Goods
        Delete.ForeignKey().FromTable("goods").ForeignColumn("UnitId").ToTable("units").PrimaryColumn("Id");
        
        // Drop Units table (assume full resync will populate Measures)
        Delete.Table("units");
        
        // Create Measures table
        Create.Table("measures")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("ShortName").AsString().Nullable()
            .WithColumn("Okei").AsInt64().Nullable()
            
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("IsArchive").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
            
        // Rename column in Goods and add new FK
        Rename.Column("UnitId").OnTable("goods").To("MeasureId");
        
        Create.ForeignKey()
            .FromTable("goods").ForeignColumn("MeasureId")
            .ToTable("measures").PrimaryColumn("Id");

        // 2. Countries
        Create.Table("countries")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("FullName").AsString().Nullable()
            .WithColumn("InternationalName").AsString().Nullable()
            .WithColumn("Code").AsString().Nullable()
            .WithColumn("Alfa2").AsString().Nullable()
            .WithColumn("Alfa3").AsString().Nullable()
            
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        // 3. Currencies
        Create.Table("currencies")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("ShortName").AsString().Nullable()
            .WithColumn("NameIso").AsString().Nullable()
            .WithColumn("CodeIso").AsInt64().Nullable()
            
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("IsUser").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("UserValue").AsDecimal().Nullable()
            
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        // 4. GoodsMeasures
        Create.Table("goods_measures")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("GoodId").AsInt64().NotNullable().ForeignKey("goods", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("MeasureId").AsInt64().NotNullable().ForeignKey("measures", "Id").OnDelete(System.Data.Rule.Cascade)
            
            .WithColumn("IsBase").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Coefficient").AsDecimal().NotNullable().WithDefaultValue(0)
            .WithColumn("MarkingPack").AsBoolean().NotNullable().WithDefaultValue(false)
            
            .WithColumn("BusinessRuUpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
            
        // Index for lookups
        Create.Index("IX_GoodsMeasures_GoodId").OnTable("goods_measures").OnColumn("GoodId");
    }

    public override void Down()
    {
        Delete.Table("goods_measures");
        Delete.Table("currencies");
        Delete.Table("countries");
        
        Delete.ForeignKey().FromTable("goods").ForeignColumn("MeasureId").ToTable("measures").PrimaryColumn("Id");
        Rename.Column("MeasureId").OnTable("goods").To("UnitId");
        
        Delete.Table("measures");
        
        // Restore Units (schema only, data is lost)
        Create.Table("units")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("FullName").AsString().Nullable()
            .WithColumn("Code").AsString().Nullable()
            .WithColumn("LastSyncedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
            
        Create.ForeignKey().FromTable("goods").ForeignColumn("UnitId").ToTable("units").PrimaryColumn("Id");
    }
}
