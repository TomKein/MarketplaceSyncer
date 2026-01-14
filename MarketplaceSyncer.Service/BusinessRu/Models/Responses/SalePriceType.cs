using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record SalePriceType(
    [property: JsonPropertyName("id")] 
    string Id,
    
    [property: JsonPropertyName("name")] 
    string Name,
    
    [property: JsonPropertyName("responsible_employee_id")] 
    string? ResponsibleEmployeeId = null,
    
    [property: JsonPropertyName("organization_id")] 
    string? OrganizationId = null,
    
    [property: JsonPropertyName("currency_id")] 
    string? CurrencyId = null,
    
    [property: JsonPropertyName("owner_employee_id")] 
    string? OwnerEmployeeId = null,
    
    [property: JsonPropertyName("archive")] 
    bool Archive = false);
