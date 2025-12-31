using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;

namespace BusinessRu.ApiClient;

public sealed class BusinessRuClient : IDisposable
{
	private readonly HttpClient _httpClient;

	private readonly string _appId;

	private readonly string _secret;

	private readonly string _baseUrl;

	private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

	private readonly ILogger<BusinessRuClient>? _logger;

	private readonly RateLimiter _rateLimiter;

	private string _token = string.Empty;

	private DateTime _tokenExpiry = DateTime.MinValue;

	public BusinessRuClient(HttpClient httpClient, string appId, string secret, string baseUrl, ILogger<BusinessRuClient>? logger = null, RateLimiterOptions? rateLimiterOptions = null)
	{
		ArgumentNullException.ThrowIfNull(httpClient, "httpClient");
		ArgumentException.ThrowIfNullOrWhiteSpace(appId, "appId");
		ArgumentException.ThrowIfNullOrWhiteSpace(secret, "secret");
		ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, "baseUrl");
		_httpClient = httpClient;
		_appId = appId;
		_secret = secret;
		_baseUrl = (baseUrl.EndsWith('/') ? baseUrl : (baseUrl + "/"));
		_logger = logger;
		_rateLimiter = new RateLimiter(rateLimiterOptions ?? new RateLimiterOptions(), logger);
	}

	public async Task<TResponse> RequestAsync<TRequest, TResponse>(HttpMethod method, string model, TRequest request, CancellationToken ct = default(CancellationToken)) where TRequest : class where TResponse : class
	{
		ArgumentNullException.ThrowIfNull(method, "method");
		ArgumentException.ThrowIfNullOrWhiteSpace(model, "model");
		ArgumentNullException.ThrowIfNull(request, "request");
		await _semaphore.WaitAsync(ct);
		try
		{
			return await ExecuteWithRetryAsync(() => PerformRequestAsync<TRequest, TResponse>(method, model, request, ct), ct);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	private async Task<TResponse> PerformRequestAsync<TRequest, TResponse>(HttpMethod method, string model, TRequest request, CancellationToken ct) where TRequest : class where TResponse : class
	{
		await _rateLimiter.WaitAsync(ct);
		if (string.IsNullOrEmpty(_token) || DateTime.UtcNow >= _tokenExpiry)
		{
			await RefreshTokenAsync(ct);
		}
		Dictionary<string, string> parameters = SerializeToParameters(request);
		parameters["app_id"] = _appId;
		SortedDictionary<string, string> sortedParams = new SortedDictionary<string, string>(parameters, new PhpKSort());
		string signatureInput = string.Concat(str2: BuildQueryStringRaw(sortedParams), str0: _token, str1: _secret);
		string signature = ComputeMd5Hash(signatureInput);
		string queryStringEncoded = BuildQueryStringEncoded(sortedParams);
		string fullQuery = queryStringEncoded + "&app_psw=" + signature;
		string url = _baseUrl + model + ".json";
		_logger?.LogDebug("Request to {Model}: {Params}", model, string.Join(", ", sortedParams.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kvp) => kvp.Key + "=" + kvp.Value)));
		using HttpRequestMessage requestMessage = CreateHttpRequest(method, url, fullQuery);
		using HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, ct);
		HttpStatusCode statusCode = response.StatusCode;
		if ((statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden) ? true : false)
		{
			string errorContent = await response.Content.ReadAsStringAsync(ct);
			_logger?.LogWarning("Request failed with {StatusCode} for {Model}. Response: {Response}", response.StatusCode, model, errorContent);
			if (errorContent.Contains("token") || errorContent.Contains("авторизац") || errorContent.Contains("auth"))
			{
				_logger?.LogWarning("Token expired, refreshing...");
				await RefreshTokenAsync(ct);
				throw new UnauthorizedAccessException("Token expired, retry required");
			}
			throw new HttpRequestException($"Request failed with {response.StatusCode}: {errorContent}");
		}
		response.EnsureSuccessStatusCode();
		ApiResponse<TResponse> apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(await response.Content.ReadAsStringAsync(ct)) ?? throw new InvalidOperationException("Empty response from API");
		if (apiResponse.RequestCount > 0)
		{
			int delay = apiResponse.RequestCount * 3;
			await Task.Delay(delay, ct);
			if (apiResponse.RequestCount % 10 == 0)
			{
				_logger?.LogDebug("REQUEST_COUNT={Count}, delay={Delay}ms", apiResponse.RequestCount, delay);
			}
		}
		return apiResponse.Result ?? throw new InvalidOperationException("Result is null in API response");
	}

	private async Task RefreshTokenAsync(CancellationToken ct)
	{
		_logger?.LogInformation("Requesting new token...");
		Dictionary<string, string> parameters = new Dictionary<string, string> { ["app_id"] = _appId };
		SortedDictionary<string, string> sorted = new SortedDictionary<string, string>(parameters, StringComparer.Ordinal);
		string signatureInput = string.Concat(str1: BuildQueryStringRaw(sorted), str0: _secret);
		string signature = ComputeMd5Hash(signatureInput, Encoding.ASCII);
		string queryStringEncoded = BuildQueryStringEncoded(sorted);
		string url = $"{_baseUrl}repair.json?{queryStringEncoded}&app_psw={signature}";
		using HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
		string json = await response.Content.ReadAsStringAsync(ct);
		if (response.StatusCode != HttpStatusCode.OK || json.Contains("Превышение лимита"))
		{
			_token = string.Empty;
			_logger?.LogError("RepairAsync: ошибка получения токена! - {StatusCode}", response.StatusCode);
			await Task.Delay(30000, ct);
			throw new InvalidOperationException($"Failed to refresh token: {response.StatusCode}");
		}
		TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json) ?? throw new InvalidOperationException("Empty response from repair endpoint");
		_token = tokenResponse.Token ?? throw new InvalidOperationException("Token is null in repair response");
		_tokenExpiry = DateTime.UtcNow.AddHours(1.0);
		_logger?.LogInformation("RepairAsync: получен новый токен!");
	}

	private static HttpRequestMessage CreateHttpRequest(HttpMethod method, string url, string query)
	{
		if (method == HttpMethod.Get || method == HttpMethod.Delete)
		{
			return new HttpRequestMessage(method, url + "?" + query);
		}
		return new HttpRequestMessage(method, url)
		{
			Content = new StringContent(query, Encoding.UTF8, "application/x-www-form-urlencoded")
		};
	}

	private static Dictionary<string, string> SerializeToParameters<T>(T obj) where T : class
	{
		string json = JsonSerializer.Serialize(obj);
		Dictionary<string, JsonElement> source = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();
		return source.ToDictionary((KeyValuePair<string, JsonElement> kvp) => kvp.Key, (KeyValuePair<string, JsonElement> kvp) => (kvp.Value.ValueKind == JsonValueKind.String) ? (kvp.Value.GetString() ?? string.Empty) : kvp.Value.ToString());
	}

	private static string BuildQueryStringRaw(IDictionary<string, string> parameters)
	{
		return string.Join("&", parameters.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kvp) => kvp.Key + "=" + kvp.Value));
	}

	private static string BuildQueryStringEncoded(IDictionary<string, string> parameters)
	{
		return string.Join("&", parameters.Select<KeyValuePair<string, string>, string>(delegate(KeyValuePair<string, string> kvp)
		{
			string text = EncodePhpStyle(kvp.Key);
			string text2 = EncodePhpStyle(kvp.Value);
			return text + "=" + text2;
		}));
	}

	private static string EncodePhpStyle(string input)
	{
		string text = HttpUtility.UrlEncode(input);
		text = text.Replace("!", "%21").Replace("#", "%23").Replace("$", "%24")
			.Replace("&", "%26")
			.Replace("'", "%27")
			.Replace("(", "%28")
			.Replace(")", "%29")
			.Replace("*", "%2A");
		return Regex.Replace(text, "%[a-f0-9]{2}", (Match m) => m.Value.ToUpperInvariant());
	}

	private static string ComputeMd5Hash(string input, Encoding? encoding = null)
	{
		byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(input);
		byte[] inArray = MD5.HashData(bytes);
		return Convert.ToHexString(inArray).ToLowerInvariant();
	}

	private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken ct, int maxRetries = 3)
	{
		for (int attempt = 0; attempt < maxRetries; attempt++)
		{
			try
			{
				return await action();
			}
			catch (Exception ex) when (attempt < maxRetries - 1)
			{
				TimeSpan delay = TimeSpan.FromSeconds(10L);
				if (ex is UnauthorizedAccessException && ex.Message.Contains("Token expired"))
				{
					_logger?.LogDebug("Token expired, refreshing... (attempt {Attempt}/{MaxRetries})", attempt + 1, maxRetries);
				}
				else
				{
					_logger?.LogWarning(ex, "RequestAsync: ошибка запроса к бизнес.ру [{Attempt}] - {Message}", attempt, ex.Message);
				}
				await Task.Delay(delay, ct);
			}
		}
		throw new InvalidOperationException("This code should be unreachable");
	}

	public void Dispose()
	{
		_semaphore.Dispose();
		_rateLimiter.Dispose();
	}
}
