using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

[Table("groups")]
public class Group
{
    [PrimaryKey, NotNull] public int Id { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, Nullable] public int? ParentId { get; set; }
    [Column, Nullable] public DateTime? BusinessRuUpdatedAt { get; set; }
    
    [Column(DataType = LinqToDB.DataType.BinaryJson), Nullable] 
    public string? RawData { get; set; }
    
    [Column, NotNull] public DateTime LastSyncedAt { get; set; }
}

[Table("units")]
public class Unit
{
    [PrimaryKey, NotNull] public int Id { get; set; }
    [Column, NotNull] public required string Name { get; set; }
    [Column, Nullable] public string? FullName { get; set; }
    [Column, Nullable] public string? Code { get; set; }
    
    [Column, NotNull] public DateTime LastSyncedAt { get; set; }
}

[Table("goods")]
public class Good
{
    [PrimaryKey, NotNull] public int Id { get; set; }
    [Column, Nullable] public string? AccountId { get; set; }
    
    [Column, Nullable] public string? Name { get; set; }
    [Column, Nullable] public string? Code { get; set; }
    [Column, Nullable] public string? Article { get; set; }
    [Column, Nullable] public string? Description { get; set; }
    [Column, NotNull] public bool IsService { get; set; }
    [Column, NotNull] public bool IsArchive { get; set; }
    
    [Column, Nullable] public int? GroupId { get; set; }
    [Column, Nullable] public int? UnitId { get; set; }
    
    [Column, Nullable] public decimal? Price { get; set; }
    [Column, Nullable] public decimal? Quantity { get; set; }
    
    [Column, NotNull] public int SyncStatus { get; set; }
    [Column, Nullable] public string? DataHash { get; set; }
    [Column, Nullable] public DateTime? LastSyncedAt { get; set; }
    [Column, Nullable] public DateTime? BusinessRuUpdatedAt { get; set; }
    [Column, NotNull] public DateTime InternalUpdatedAt { get; set; }
    
    [Column(DataType = LinqToDB.DataType.BinaryJson), Nullable] 
    public string? RawData { get; set; }

    [Association(ThisKey = nameof(GroupId), OtherKey = nameof(Models.Group.Id))]
    public Group? Group { get; set; }
    
    [Association(ThisKey = nameof(UnitId), OtherKey = nameof(Models.Unit.Id))]
    public Unit? Unit { get; set; }
}
