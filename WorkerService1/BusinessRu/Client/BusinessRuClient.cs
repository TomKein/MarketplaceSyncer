using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using WorkerService1.BusinessRu.Http;
using WorkerService1.BusinessRu.Models.Responses;
using WorkerService1.BusinessRu.Query;

namespace WorkerService1.BusinessRu.Client;

public sealed class BusinessRuClient : IBusinessRuClient
{
    private readonly HttpClient _httpClient;
    private readonly string _appId;
    private readonly string _secret;
    private readonly string _baseUrl;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<BusinessRuClient> _logger;
    private readonly RateLimiter _rateLimiter;

    private string _token = string.Empty;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public BusinessRuClient(
        HttpClient httpClient,
        string appId,
        string secret,
        string baseUrl,
        ILogger<BusinessRuClient> logger,
        RateLimiterOptions? rateLimiterOptions = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        _httpClient = httpClient;
        _appId = appId;
        _secret = secret;
        _baseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
        _logger = logger;
        _rateLimiter = new RateLimiter(
            rateLimiterOptions ?? new RateLimiterOptions(),
            logger);
    }

    public async Task<Good[]> GetGoodsAsync(
        int? businessId = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, object>
        {
            ["resource"] = "good",
            ["fields"] = new[] { "id", "name", "part_number", "store_code", "archive" }
        };

        var filters = new Dictionary<string, object>();

        if (businessId.HasValue)
            filters["business_id"] = businessId.Value;

        if (!includeArchived)
            filters["archive"] = false;

        if (filters.Count > 0)
            query["filter"] = filters;

        var response = await QueryAsync<ApiResponse<Good[]>>(query, cancellationToken);
        return response?.Result ?? Array.Empty<Good>();
    }

    public async Task<int> CountGoodsAsync(
        bool includeArchived = false,
        int? type = 1,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>
        {
            ["count_only"] = "1"
        };

        if (!includeArchived)
            request["archive"] = "0";

        if (type.HasValue)
            request["type"] = type.Value.ToString();

        _logger.LogDebug(
            "Counting goods (archive={Archive}, type={Type})",
            includeArchived ? "all" : "0",
            type);

        var response = await RequestAsync<
            Dictionary<string, string>,
            CountResponse>(
            HttpMethod.Get,
            "goods",
            request,
            cancellationToken);

        return response.Count;
    }

    public async Task<Good> GetGoodByIdAsync(
        string goodId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goodId);

        _logger.LogDebug("Fetching good by ID: {GoodId}", goodId);

        var request = new Dictionary<string, string>
        {
            ["id"] = goodId
        };

        var response = await RequestAsync<
            Dictionary<string, string>,
            Good>(
            HttpMethod.Get,
            "good",
            request,
            cancellationToken);

        return response;
    }

    public async Task<SalePriceListGoodPrice[]> GetGoodPricesAsync(
        string goodId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goodId);

        _logger.LogDebug(
            "Fetching prices for good ID: {GoodId}",
            goodId);

        var request = new Dictionary<string, string>
        {
            ["good_id"] = goodId
        };

        if (limit.HasValue)
            request["limit"] = limit.Value.ToString();

        var response = await RequestAsync<
            Dictionary<string, string>,
            SalePriceListGoodPrice[]>(
            HttpMethod.Get,
            "salepricelistgoodprices",
            request,
            cancellationToken);

        return response;
    }

    public async Task<SalePriceList[]> GetPriceListsAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, object>
        {
            ["resource"] = "sale_price_list"
        };

        if (limit.HasValue)
            query["limit"] = limit.Value;

        var response = await QueryAsync<ApiResponse<SalePriceList[]>>(
            query,
            cancellationToken);
        
        return response?.Result ?? Array.Empty<SalePriceList>();
    }

    public async Task<SalePriceType[]> GetPriceTypesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, object>
        {
            ["resource"] = "sale_price_type"
        };

        if (limit.HasValue)
            query["limit"] = limit.Value;

        var response = await QueryAsync<ApiResponse<SalePriceType[]>>(
            query,
            cancellationToken);
        
        return response?.Result ?? Array.Empty<SalePriceType>();
    }

    public async Task UpdatePriceAsync(
        string priceId,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(priceId);

        var query = new Dictionary<string, object>
        {
            ["resource"] = "sale_price_list_good_price",
            ["id"] = priceId,
            ["price"] = price.ToString("F2")
        };

        await QueryAsync<ApiResponse<object>>(query, cancellationToken);
    }

    public async Task<int> CountAsync(
        string resource,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        var query = new Dictionary<string, object>
        {
            ["resource"] = resource,
            ["count"] = 1
        };

        if (filters is { Count: > 0 })
            query["filter"] = filters;

        _logger.LogDebug("Counting records for resource: {Resource}", resource);

        var response = await QueryAsync<CountResponse>(query, cancellationToken);
        return response.Count;
    }

    public async Task<T> QueryAsync<T>(
        Dictionary<string, object> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteWithRetryAsync(
                () => PerformQueryAsync<T>(request, cancellationToken),
                cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public IBusinessRuQuery CreateQuery() => new BusinessRuQuery();

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(
        HttpMethod method,
        string model,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(request);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteWithRetryAsync(
                () => PerformLegacyRequestAsync<TRequest, TResponse>(
                    method,
                    model,
                    request,
                    cancellationToken),
                cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<TResponse> PerformLegacyRequestAsync<TRequest, TResponse>(
        HttpMethod method,
        string model,
        TRequest request,
        CancellationToken ct)
        where TRequest : class
        where TResponse : class
    {
        await _rateLimiter.WaitAsync(ct);

        if (string.IsNullOrEmpty(_token) || DateTime.UtcNow >= _tokenExpiry)
            await RefreshTokenAsync(ct);

        var parameters = SerializeToStringParameters(request);
        parameters["app_id"] = _appId;

        var sortedParams = new SortedDictionary<string, string>(
            parameters,
            new PhpKSort());

        var queryStringEncoded = BuildQueryStringEncoded(sortedParams);
        var signatureInput = _token + _secret + queryStringEncoded;
        var signature = ComputeMd5Hash(signatureInput);
        var fullQuery = $"{queryStringEncoded}&app_psw={signature}";
        var url = $"{_baseUrl}{model}.json";

        _logger.LogDebug("Legacy request to {Model}", model);

        using var requestMessage = CreateHttpRequest(method, url, fullQuery);
        using var response = await _httpClient.SendAsync(requestMessage, ct);

        await HandleAuthenticationErrorsAsync(response, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(ct);
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(payload)
            ?? throw new InvalidOperationException("Empty response from API");

        await HandleRateLimitingFromApiResponse(apiResponse.RequestCount, ct);

        return apiResponse.Result
            ?? throw new InvalidOperationException("Result is null in API response");
    }

    private async Task<T> PerformQueryAsync<T>(
        Dictionary<string, object> request,
        CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct);

        if (string.IsNullOrEmpty(_token) || DateTime.UtcNow >= _tokenExpiry)
            await RefreshTokenAsync(ct);

        var parameters = SerializeToParameters(request);
        parameters["app_id"] = _appId;

        var sortedParams = new SortedDictionary<string, string>(
            parameters,
            new PhpKSort());
        
        var queryStringEncoded = BuildQueryStringEncoded(sortedParams);
        var signatureInput = _token + _secret + queryStringEncoded;
        var signature = ComputeMd5Hash(signatureInput);
        var fullQuery = $"{queryStringEncoded}&app_psw={signature}";
        var url = $"{_baseUrl}repair.json";

        _logger.LogDebug(
            "Query request: {Resource}",
            request.GetValueOrDefault("resource"));

        using var requestMessage = CreateHttpRequest(HttpMethod.Post, url, fullQuery);
        using var response = await _httpClient.SendAsync(requestMessage, ct);

        await HandleAuthenticationErrorsAsync(response, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<T>(payload)
            ?? throw new InvalidOperationException("Empty response from API");

        await HandleRateLimitingAsync(payload, ct);

        return result;
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        _logger.LogInformation("Requesting new token");

        var parameters = new Dictionary<string, string> { ["app_id"] = _appId };
        var sorted = new SortedDictionary<string, string>(
            parameters,
            StringComparer.Ordinal);

        var signatureInput = _secret + BuildQueryStringRaw(sorted);
        var signature = ComputeMd5Hash(signatureInput, Encoding.ASCII);
        var queryStringEncoded = BuildQueryStringEncoded(sorted);
        var url = $"{_baseUrl}repair.json?{queryStringEncoded}&app_psw={signature}";

        using var response = await _httpClient.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode != HttpStatusCode.OK 
            || json.Contains("Превышение лимита"))
        {
            _token = string.Empty;
            _logger.LogError(
                "Failed to refresh token. Status: {StatusCode}",
                response.StatusCode);
            
            await Task.Delay(30000, ct);
            throw new InvalidOperationException(
                $"Failed to refresh token: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json)
            ?? throw new InvalidOperationException(
                "Empty response from repair endpoint");

        _token = tokenResponse.Token
            ?? throw new InvalidOperationException(
                "Token is null in repair response");

        _tokenExpiry = DateTime.UtcNow.AddHours(1);
        _logger.LogInformation("New token acquired successfully");
    }

    private async Task HandleAuthenticationErrorsAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var statusCode = response.StatusCode;
        
        if (statusCode != HttpStatusCode.Unauthorized 
            && statusCode != HttpStatusCode.Forbidden)
            return;

        var errorContent = await response.Content.ReadAsStringAsync(ct);
        
        _logger.LogWarning(
            "Request failed with {StatusCode}. Response: {Response}",
            response.StatusCode,
            errorContent);

        if (errorContent.Contains("token") 
            || errorContent.Contains("авторизац") 
            || errorContent.Contains("auth"))
        {
            _logger.LogWarning("Token expired, refreshing");
            await RefreshTokenAsync(ct);
            throw new UnauthorizedAccessException("Token expired, retry required");
        }

        throw new HttpRequestException(
            $"Request failed with {response.StatusCode}: {errorContent}");
    }

    private async Task HandleRateLimitingAsync(string payload, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            
            if (!doc.RootElement.TryGetProperty("request_count", out var countElement))
                return;

            var requestCount = countElement.GetInt32();
            
            if (requestCount <= 0)
                return;

            var delay = requestCount * 3;
            await Task.Delay(delay, ct);

            if (requestCount % 10 == 0)
            {
                _logger.LogDebug(
                    "Rate limiting: request_count={Count}, delay={Delay}ms",
                    requestCount,
                    delay);
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors for rate limit handling
        }
    }

    private async Task HandleRateLimitingFromApiResponse(
        int requestCount,
        CancellationToken ct)
    {
        if (requestCount <= 0)
            return;

        var delay = requestCount * 3;
        await Task.Delay(delay, ct);

        if (requestCount % 10 == 0)
        {
            _logger.LogDebug(
                "Rate limiting: request_count={Count}, delay={Delay}ms",
                requestCount,
                delay);
        }
    }

    private static HttpRequestMessage CreateHttpRequest(
        HttpMethod method,
        string url,
        string query)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Delete)
            return new HttpRequestMessage(method, $"{url}?{query}");

        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(
                query,
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
    }

    private static Dictionary<string, string> SerializeToParameters(
        Dictionary<string, object> obj)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var kvp in obj)
        {
            FlattenParameter(kvp.Key, kvp.Value, result);
        }
        
        return result;
    }

    private static void FlattenParameter(
        string key,
        object value,
        Dictionary<string, string> result)
    {
        if (value is null)
        {
            result[key] = string.Empty;
            return;
        }

        var json = JsonSerializer.Serialize(value);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                result[key] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                result[key] = element.ToString();
                break;

            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var nestedKey = $"{key}[{prop.Name}]";
                    FlattenParameter(nestedKey, prop.Value, result);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var nestedKey = $"{key}[{index}]";
                    FlattenParameter(nestedKey, item, result);
                    index++;
                }
                break;

            default:
                result[key] = element.ToString();
                break;
        }
    }

    private static Dictionary<string, string> SerializeToStringParameters<T>(T obj)
        where T : class
    {
        var json = JsonSerializer.Serialize(obj);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? new Dictionary<string, JsonElement>();

        return dict.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ValueKind == JsonValueKind.String
                ? kvp.Value.GetString() ?? string.Empty
                : kvp.Value.ToString());
    }

    private static string BuildQueryStringRaw(IDictionary<string, string> parameters)
    {
        return string.Join(
            "&",
            parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }

    private static string BuildQueryStringEncoded(
        IDictionary<string, string> parameters)
    {
        return string.Join(
            "&",
            parameters.Select(kvp =>
            {
                var key = EncodePhpStyle(kvp.Key);
                var value = EncodePhpStyle(kvp.Value);
                return $"{key}={value}";
            }));
    }

    private static string EncodePhpStyle(string input)
    {
        var encoded = HttpUtility.UrlEncode(input);
        encoded = encoded
            .Replace("!", "%21")
            .Replace("#", "%23")
            .Replace("$", "%24")
            .Replace("&", "%26")
            .Replace("'", "%27")
            .Replace("(", "%28")
            .Replace(")", "%29")
            .Replace("*", "%2A");

        return Regex.Replace(
            encoded,
            "%[a-f0-9]{2}",
            m => m.Value.ToUpperInvariant());
    }

    private static string ComputeMd5Hash(string input, Encoding? encoding = null)
    {
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct,
        int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(10);

                if (ex is UnauthorizedAccessException 
                    && ex.Message.Contains("Token expired"))
                {
                    _logger.LogDebug(
                        "Token expired, refreshing (attempt {Attempt}/{MaxRetries})",
                        attempt + 1,
                        maxRetries);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Request error (attempt {Attempt}/{MaxRetries}): {Message}",
                        attempt + 1,
                        maxRetries,
                        ex.Message);
                }

                await Task.Delay(delay, ct);
            }
        }

        throw new InvalidOperationException("Max retries exceeded");
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        _rateLimiter.Dispose();
    }
}
