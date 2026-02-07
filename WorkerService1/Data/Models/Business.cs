using LinqToDB.Mapping;

namespace WorkerService1.Data.Models;

/// <summary>
/// Организация в Business.ru
/// </summary>
[Table("businesses")]
public class Business
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("external_id"), NotNull]
    public string ExternalId { get; set; } = string.Empty;

    [Column("organization_id"), NotNull]
    public string OrganizationId { get; set; } = string.Empty;

    [Column("name"), NotNull]
    public string Name { get; set; } = string.Empty;

    [Column("api_url"), NotNull]
    public string ApiUrl { get; set; } = string.Empty;

    [Column("app_id"), NotNull]
    public string AppId { get; set; } = string.Empty;

    [Column("secret_encrypted"), NotNull]
    public string SecretEncrypted { get; set; } = string.Empty;

    [Column("is_active"), NotNull]
    public bool IsActive { get; set; } = true;

    [Column("created_at"), NotNull]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at"), NotNull]
    public DateTimeOffset UpdatedAt { get; set; }
}
