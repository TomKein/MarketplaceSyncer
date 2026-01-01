namespace WorkerService1.BusinessRu.Http;

public sealed class RateLimiter : IDisposable
{
    private readonly RateLimiterOptions _options;
    private readonly ILogger? _logger;
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RateLimiter(RateLimiterOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var cutoffTime = DateTime.UtcNow - _options.TimeWindow;
            
            while (_requestTimestamps.Count > 0 
                   && _requestTimestamps.Peek() < cutoffTime)
            {
                _requestTimestamps.Dequeue();
            }

            var currentCount = _requestTimestamps.Count;
            
            if (currentCount >= _options.MaxRequests)
            {
                var oldestRequest = _requestTimestamps.Peek();
                var waitTime = oldestRequest + _options.TimeWindow 
                               - DateTime.UtcNow;
                
                if (waitTime > TimeSpan.Zero)
                {
                    _logger?.LogWarning(
                        "Rate limit reached ({Count}/{Max}). Waiting {Wait}ms",
                        currentCount,
                        _options.MaxRequests,
                        waitTime.TotalMilliseconds);
                    
                    await Task.Delay(waitTime, ct);
                    
                    cutoffTime = DateTime.UtcNow - _options.TimeWindow;
                    
                    while (_requestTimestamps.Count > 0 
                           && _requestTimestamps.Peek() < cutoffTime)
                    {
                        _requestTimestamps.Dequeue();
                    }
                    
                    currentCount = _requestTimestamps.Count;
                }
            }

            var threshold = (int)(_options.MaxRequests 
                                  * _options.ThrottleThreshold);
            
            if (currentCount >= threshold)
            {
                var progress = (double)(currentCount - threshold) 
                               / (_options.MaxRequests - threshold);
                
                var delayMs = Math.Max(
                    _options.MinThrottleDelay.TotalMilliseconds,
                    _options.TimeWindow.TotalMilliseconds 
                    / _options.MaxRequests * progress);
                
                var delay = TimeSpan.FromMilliseconds(delayMs);
                
                if (currentCount % 50 == 0)
                {
                    _logger?.LogInformation(
                        "Throttling active: {Count}/{Max} requests. Delay: {Delay}ms",
                        currentCount,
                        _options.MaxRequests,
                        delay.TotalMilliseconds);
                }
                
                await Task.Delay(delay, ct);
            }

            _requestTimestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _lock.Release();
        }
    }

    public int GetCurrentCount()
    {
        var cutoffTime = DateTime.UtcNow - _options.TimeWindow;
        return _requestTimestamps.Count(t => t >= cutoffTime);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
