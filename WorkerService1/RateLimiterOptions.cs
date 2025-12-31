namespace BusinessRu.ApiClient;

public class RateLimiterOptions
{
    public int MaxRequests { get; set; } = 500;
    public TimeSpan TimeWindow { get; set; } = TimeSpan.FromSeconds(60);
    public double ThrottleThreshold { get; set; } = 0.8;
    public TimeSpan MinThrottleDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}