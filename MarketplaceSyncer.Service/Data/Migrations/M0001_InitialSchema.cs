using FluentMigrator;
using System;

namespace MarketplaceSyncer.Service.Data.Migrations;

[Migration(1, "Initial schema with Goods and dicts")]
public class M0001_InitialSchema : Migration
{
    public override void Up()
    {
        // 1. Dictionaries (Справочники)
        
        // Groups (Группы товаров)
        Create.Table("groups")
            .WithColumn("Id").AsInt32().PrimaryKey() 
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("ParentId").AsInt32().Nullable().ForeignKey("groups", "Id")
            .WithColumn("BusinessRuUpdatedAt").AsDateTime().Nullable()
            // Sync Meta
            .WithColumn("RawData").AsCustom("jsonb").Nullable()
            .WithColumn("LastSyncedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        // Units (Единицы измерения)
        Create.Table("units")
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("FullName").AsString().Nullable()
            .WithColumn("Code").AsString().Nullable() 
            // Sync Meta
            .WithColumn("LastSyncedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        // 2. Main Table - Goods (Товары)
        Create.Table("goods")
            // Identifiers
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("AccountId").AsString().Nullable() 
            
            // Core attributes
            .WithColumn("Name").AsString().Nullable()
            .WithColumn("Code").AsString().Nullable().Indexed() 
            .WithColumn("Article").AsString().Nullable().Indexed() 
            .WithColumn("Description").AsString().Nullable()
            .WithColumn("IsService").AsBoolean().WithDefaultValue(false)
            .WithColumn("IsArchive").AsBoolean().WithDefaultValue(false)
            
            // Relations
            .WithColumn("GroupId").AsInt32().Nullable().ForeignKey("groups", "Id")
            .WithColumn("UnitId").AsInt32().Nullable().ForeignKey("units", "Id")
            
            // Pricing & Stock
            .WithColumn("Price").AsDecimal().Nullable() 
            .WithColumn("Quantity").AsDecimal().Nullable()
            
            // Sync State Machine
            .WithColumn("SyncStatus").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DataHash").AsString().Nullable() // MD5/SHA хеш значимых полей для детекта изменений
            .WithColumn("LastSyncedAt").AsDateTime().Nullable()
            .WithColumn("BusinessRuUpdatedAt").AsDateTime().Nullable() // updated timestamp from API
            .WithColumn("InternalUpdatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            
            // The Safety Net
            .WithColumn("RawData").AsCustom("jsonb").Nullable();

        // Indexes for performance
        Create.Index("IX_Goods_SyncStatus").OnTable("goods").OnColumn("SyncStatus");
        Create.Index("IX_Goods_BusinessRuUpdatedAt").OnTable("goods").OnColumn("BusinessRuUpdatedAt");
    }

    public override void Down()
    {
        Delete.Table("goods");
        Delete.Table("units");
        Delete.Table("groups");
    }
}
