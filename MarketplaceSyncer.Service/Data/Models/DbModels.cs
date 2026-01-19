using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

[Table("groups")]
public class Group
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, Nullable] public long? ParentId { get; set; }
    
    [Column, Nullable] public string? Description { get; set; }
    [Column, Nullable] public int? DefaultOrder { get; set; }
    [Column, NotNull] public bool IsDeleted { get; set; }

    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    
    [Column(DataType = LinqToDB.DataType.BinaryJson), Nullable] 
    public string? RawData { get; set; }
    
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }
}

[Table("units")]
public class Unit
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, Nullable] public string? FullName { get; set; }
    [Column, Nullable] public string? Code { get; set; }
    
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }
}

[Table("goods")]
public class Good
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    
    [Column, Nullable] public string? Name { get; set; }
    [Column, Nullable] public string? Code { get; set; }
    [Column, Nullable] public string? Article { get; set; }
    [Column, Nullable] public string? Description { get; set; }
    [Column, NotNull] public bool IsService { get; set; }
    [Column, NotNull] public bool IsArchive { get; set; }
    
    [Column, Nullable] public long? GroupId { get; set; }
    [Column, Nullable] public long? UnitId { get; set; }
    
    [Column, Nullable] public decimal? Price { get; set; }
    [Column, Nullable] public decimal? Quantity { get; set; }
    
    [Column, NotNull] public int SyncStatus { get; set; }
    [Column, Nullable] public string? DataHash { get; set; }
    [Column, Nullable] public DateTimeOffset? LastSyncedAt { get; set; }
    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTimeOffset InternalUpdatedAt { get; set; }
    
    [Column(DataType = LinqToDB.DataType.BinaryJson), Nullable] 
    public string? RawData { get; set; }

    [Association(ThisKey = nameof(GroupId), OtherKey = nameof(Models.Group.Id))]
    public Group? Group { get; set; }
    
    [Association(ThisKey = nameof(UnitId), OtherKey = nameof(Models.Unit.Id))]
    public Unit? Unit { get; set; }
}

[Table("stores")]
public class Store
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, Nullable] public string? Address { get; set; }
    
    [Column, NotNull] public bool IsArchive { get; set; }
    [Column, NotNull] public bool IsDeleted { get; set; }
    [Column, NotNull] public bool DenyNegativeBalance { get; set; }
    
    [Column, Nullable] public long? ResponsibleEmployeeId { get; set; }
    [Column, Nullable] public int? DebitType { get; set; }
    
    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }
}

[Table("store_goods")]
public class StoreGood
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    
    [Column, NotNull] public long StoreId { get; set; }
    [Column, NotNull] public long GoodId { get; set; }
    [Column, Nullable] public long? ModificationId { get; set; }
    
    [Column, NotNull] public decimal Amount { get; set; }
    [Column, NotNull] public decimal Reserved { get; set; }
    [Column, NotNull] public decimal RemainsMin { get; set; }
    
    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }

    [Association(ThisKey = nameof(StoreId), OtherKey = nameof(Models.Store.Id))]
    public Store? Store { get; set; }

    [Association(ThisKey = nameof(GoodId), OtherKey = nameof(Models.Good.Id))]
    public Good? Good { get; set; }
}

[Table("attributes")]
public class Attribute
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, NotNull] public bool Selectable { get; set; }
    [Column, NotNull] public bool Archive { get; set; }
    [Column, Nullable] public string? Description { get; set; }
    [Column, NotNull] public long Sort { get; set; }
    [Column, NotNull] public bool Deleted { get; set; }
    
    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }
}

[Table("attribute_values")]
public class AttributeValue
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    [Column, NotNull] public long AttributeId { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, NotNull] public long Sort { get; set; }
    
    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }

    [Association(ThisKey = nameof(AttributeId), OtherKey = nameof(Models.Attribute.Id))]
    public Attribute? Attribute { get; set; }
}

[Table("good_attributes")]
public class GoodAttribute
{
    [PrimaryKey, NotNull] public long Id { get; set; }
    [Column, NotNull] public long GoodId { get; set; }
    [Column, NotNull] public long AttributeId { get; set; }
    [Column, Nullable] public long? ValueId { get; set; }
    [Column, Nullable] public string? Value { get; set; }
    
    [Column, Nullable] public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTimeOffset LastSyncedAt { get; set; }

    [Association(ThisKey = nameof(GoodId), OtherKey = nameof(Models.Good.Id))]
    public Good? Good { get; set; }

    [Association(ThisKey = nameof(AttributeId), OtherKey = nameof(Models.Attribute.Id))]
    public Attribute? Attribute { get; set; }
    
    [Association(ThisKey = nameof(ValueId), OtherKey = nameof(Models.AttributeValue.Id))]
    public AttributeValue? AttributeValue { get; set; }
}
