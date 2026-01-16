using LinqToDB.Mapping;

namespace MarketplaceSyncer.Service.Data.Models;

/// <summary>
/// Cached image for a good (downloaded from Business.ru)
/// </summary>
[Table("good_images")]
public class GoodImage
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public long Id { get; set; }

    [Column("GoodId"), NotNull]
    public long GoodId { get; set; }

    [Column("Name"), Nullable]
    public string? Name { get; set; }

    [Column("Url"), NotNull]
    public string Url { get; set; } = string.Empty;

    [Column("Data"), Nullable]
    public byte[]? Data { get; set; }

    [Column("ContentType"), Nullable]
    public string? ContentType { get; set; }

    /// <summary>
    /// MD5 hash of image data for change detection
    /// </summary>
    [Column("Hash"), NotNull]
    public string Hash { get; set; } = string.Empty;

    [Column("Position"), NotNull]
    public int Position { get; set; }

    /// <summary>
    /// When image was last updated in Business.ru
    /// </summary>
    [Column("BusinessRuUpdatedAt"), Nullable]
    public DateTime? BusinessRuUpdatedAt { get; set; }

    /// <summary>
    /// When image was downloaded to our database
    /// </summary>
    [Column("DownloadedAt"), NotNull]
    public DateTime DownloadedAt { get; set; }
}
