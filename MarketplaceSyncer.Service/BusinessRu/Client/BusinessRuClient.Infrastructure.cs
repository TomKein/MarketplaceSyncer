using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;

namespace MarketplaceSyncer.Service.BusinessRu.Client;

/// <summary>
/// Инфраструктурный код: HTTP-запросы, аутентификация, retry, сериализация.
/// </summary>
public sealed partial class BusinessRuClient
{
    /// <summary>
    /// Универсальный метод выполнения REST-запросов к API.
    /// Включает retry с экспоненциальным backoff и автоматическое обновление токена.
    /// </summary>
    public async Task<TResponse> RequestAsync<TRequest, TResponse>(
        HttpMethod method,
        string endpoint,
        TRequest request,
        CancellationToken ct = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(request);

        await _semaphore.WaitAsync(ct);
        try
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                if (string.IsNullOrEmpty(_token) || DateTime.UtcNow >= _tokenExpiry)
                    await RefreshTokenAsync(ct);

                var (statusCode, payload) = await SendRequestAsync(method, endpoint, request, ct);

                if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
                {
                    _token = string.Empty;
                    throw new UnauthorizedAccessException($"Ошибка авторизации: {statusCode}");
                }

                if (!IsSuccess(statusCode))
                    throw new HttpRequestException($"Ошибка {statusCode} от {endpoint}: {payload}");

                try
                {
                    var response = JsonSerializer.Deserialize<ApiResponse<TResponse>>(payload, _jsonOptions)
                        ?? throw new InvalidOperationException($"Пустой ответ от {endpoint}");

                    if (response.RequestCount > 0)
                        await Task.Delay(response.RequestCount * 3, ct);

                    return response.Result
                        ?? throw new InvalidOperationException($"Result is null от {endpoint}");
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Ошибка десериализации от {Endpoint}. Payload: {Payload}", endpoint, payload);
                    throw;
                }
            }, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #region HTTP & Auth

    private async Task<(HttpStatusCode, string)> SendRequestAsync<T>(
        HttpMethod method, string endpoint, T request, CancellationToken ct) where T : class
    {
        await _rateLimiter.WaitAsync(ct);

        var parameters = SerializeToStringParameters(request);
        parameters["app_id"] = _appId;

        var sorted = new SortedDictionary<string, string>(parameters, new PhpKSort());
        var query = BuildQueryStringEncoded(sorted);
        var signature = ComputeMd5Hash(_token + _secret + query);
        var fullQuery = $"{query}&app_psw={signature}";
        var url = $"{_baseUrl}{endpoint}.json";

        _logger.LogDebug("{Method} {Endpoint}", method.Method, endpoint);

        using var httpRequest = CreateHttpRequest(method, url, fullQuery);
        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        return (response.StatusCode, payload);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        var parameters = new Dictionary<string, string> { ["app_id"] = _appId };
        var sorted = new SortedDictionary<string, string>(parameters, StringComparer.Ordinal);
        var signature = ComputeMd5Hash(_secret + BuildQueryStringRaw(sorted), Encoding.ASCII);
        var url = $"{_baseUrl}repair.json?{BuildQueryStringEncoded(sorted)}&app_psw={signature}";

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            _logger.LogDebug("Запрос токена ({Attempt}/{Max})", attempt, _maxRetries);

            using var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.OK && !json.Contains("Превышение лимита"))
            {
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOptions)
                    ?? throw new InvalidOperationException("Пустой ответ от repair");

                _token = tokenResponse.Token ?? throw new InvalidOperationException("Token is null");
                _tokenExpiry = DateTime.UtcNow.AddHours(1);
                _logger.LogInformation("Токен получен");
                return;
            }

            _token = string.Empty;
            if (attempt < _maxRetries)
            {
                var delay = _retryBaseDelayMs * (1 << (attempt - 1));
                _logger.LogWarning("Ошибка токена ({Status}). Повтор через {Delay}мс", response.StatusCode, delay);
                await Task.Delay(delay, ct);
            }
        }

        throw new InvalidOperationException($"Не удалось получить токен после {_maxRetries} попыток");
    }

    #endregion

    #region Retry

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _maxRetries && IsRetryable(ex))
            {
                var delay = _retryBaseDelayMs * (1 << (attempt - 1));
                _logger.LogWarning(ex, "Попытка {Attempt}/{Max} неудачна. Повтор через {Delay}мс", 
                    attempt, _maxRetries, delay);
                await Task.Delay(delay, ct);
            }
        }

        return await operation();
    }

    private static bool IsSuccess(HttpStatusCode code) => (int)code >= 200 && (int)code < 300;

    private static bool IsRetryable(Exception ex) =>
        ex is UnauthorizedAccessException or HttpRequestException or TimeoutException;

    #endregion

    #region HTTP Helpers

    private static HttpRequestMessage CreateHttpRequest(HttpMethod method, string url, string query)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Delete)
            return new HttpRequestMessage(method, $"{url}?{query}");

        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(query, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
    }

    #endregion

    #region Serialization

    private static Dictionary<string, string> SerializeToStringParameters<T>(T obj) where T : class
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

    private static string BuildQueryStringRaw(IDictionary<string, string> parameters) =>
        string.Join("&", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));

    private static string BuildQueryStringEncoded(IDictionary<string, string> parameters) =>
        string.Join("&", parameters.Select(kvp => $"{EncodePhpStyle(kvp.Key)}={EncodePhpStyle(kvp.Value)}"));

    private static string EncodePhpStyle(string input)
    {
        var encoded = HttpUtility.UrlEncode(input)
            .Replace("!", "%21")
            .Replace("#", "%23")
            .Replace("$", "%24")
            .Replace("&", "%26")
            .Replace("'", "%27")
            .Replace("(", "%28")
            .Replace(")", "%29")
            .Replace("*", "%2A");

        return Regex.Replace(encoded, "%[a-f0-9]{2}", m => m.Value.ToUpperInvariant());
    }

    private static string ComputeMd5Hash(string input, Encoding? encoding = null)
    {
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion

    #region PHP-compatible sorting

    private sealed class PhpKSort : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            bool xIsNumeric = int.TryParse(x, out int xNum);
            bool yIsNumeric = int.TryParse(y, out int yNum);

            if (xIsNumeric && yIsNumeric)
                return xNum.CompareTo(yNum);

            if (xIsNumeric) return -1;
            if (yIsNumeric) return 1;

            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }

    #endregion
}
