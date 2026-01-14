namespace MarketplaceSyncer.Service.BusinessRu.Http;

public class RateLimiterOptions
{
    public int MaxRequests { get; set; } = 300;
    
    public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(5);
    
    public double ThrottleThreshold { get; set; } = 0.8;
    
    public TimeSpan MinThrottleDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
