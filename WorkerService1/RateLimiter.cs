using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BusinessRu.ApiClient;

public sealed class RateLimiter : IDisposable
{
	private readonly RateLimiterOptions _options;

	private readonly ILogger? _logger;

	private readonly Queue<DateTime> _requestTimestamps = new Queue<DateTime>();

	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

	public RateLimiter(RateLimiterOptions options, ILogger? logger = null)
	{
		_options = options;
		_logger = logger;
	}

	public async Task WaitAsync(CancellationToken ct = default(CancellationToken))
	{
		await _lock.WaitAsync(ct);
		try
		{
			DateTime cutoffTime = DateTime.UtcNow - _options.TimeWindow;
			while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoffTime)
			{
				_requestTimestamps.Dequeue();
			}
			int currentCount = _requestTimestamps.Count;
			if (currentCount >= _options.MaxRequests)
			{
				DateTime oldestRequest = _requestTimestamps.Peek();
				TimeSpan waitTime = oldestRequest + _options.TimeWindow - DateTime.UtcNow;
				if (waitTime > TimeSpan.Zero)
				{
					_logger?.LogWarning("Rate limit reached ({Count}/{Max}). Waiting {Wait}ms...", currentCount, _options.MaxRequests, waitTime.TotalMilliseconds);
					await Task.Delay(waitTime, ct);
					cutoffTime = DateTime.UtcNow - _options.TimeWindow;
					while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoffTime)
					{
						_requestTimestamps.Dequeue();
					}
					currentCount = _requestTimestamps.Count;
				}
			}
			int threshold = (int)((double)_options.MaxRequests * _options.ThrottleThreshold);
			if (currentCount >= threshold)
			{
				double progress = (double)(currentCount - threshold) / (double)(_options.MaxRequests - threshold);
				TimeSpan delay = TimeSpan.FromMilliseconds(Math.Max(val2: _options.TimeWindow.TotalMilliseconds / (double)_options.MaxRequests * progress, val1: _options.MinThrottleDelay.TotalMilliseconds));
				if (currentCount % 50 == 0)
				{
					_logger?.LogInformation("Throttling active: {Count}/{Max} requests. Delay: {Delay}ms", currentCount, _options.MaxRequests, delay.TotalMilliseconds);
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
		DateTime cutoffTime = DateTime.UtcNow - _options.TimeWindow;
		return _requestTimestamps.Count((DateTime t) => t >= cutoffTime);
	}

	public void Dispose()
	{
		_lock.Dispose();
	}
}
