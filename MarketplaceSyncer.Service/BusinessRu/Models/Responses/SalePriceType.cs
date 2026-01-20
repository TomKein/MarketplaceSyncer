using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Models.Responses;

public sealed record SalePriceType(
    [property: JsonPropertyName("id")] 
    long Id,
    
    [property: JsonPropertyName("name")] 
    string Name,
    
    [property: JsonPropertyName("responsible_employee_id")] 
    long? ResponsibleEmployeeId = null,
    
    [property: JsonPropertyName("organization_id")] 
    long? OrganizationId = null,
    
    [property: JsonPropertyName("currency_id")] 
    long? CurrencyId = null,
    
    [property: JsonPropertyName("owner_employee_id")] 
    long? OwnerEmployeeId = null,
    
    [property: JsonPropertyName("archive")] 
    bool Archive = false);
