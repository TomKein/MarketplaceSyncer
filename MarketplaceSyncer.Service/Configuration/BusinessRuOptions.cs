namespace MarketplaceSyncer.Service.Configuration;

public class BusinessRuOptions
{
    public const string SectionName = "BusinessRu";

    public required string AppId { get; set; }
    public required string Secret { get; set; }
    public required string BaseUrl { get; set; }
    public required string ResponsibleEmployeeId { get; set; }
    public required string OrganizationId { get; set; }
    public int RateLimitRequestCount { get; set; } = 300;
    public int RateLimitTimeWindowMs { get; set; } = 300000; // 5 min
    
    // Retry configuration
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 1000; // 1 секунда базовая задержка
}
