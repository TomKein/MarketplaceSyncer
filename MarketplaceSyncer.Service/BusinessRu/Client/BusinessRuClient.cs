using System.Text.Json;
using System.Text.Json.Serialization;
using MarketplaceSyncer.Service.BusinessRu.Http;

namespace MarketplaceSyncer.Service.BusinessRu.Client;

/// <summary>
/// Клиент для работы с API Business.ru.
/// </summary>
public sealed partial class BusinessRuClient : IBusinessRuClient
{
    public Query.IBusinessRuQuery CreateQuery()
    {
        return new Query.BusinessRuQuery();
    }

    private readonly HttpClient _httpClient;
    private readonly string _appId;
    private readonly string _secret;
    private readonly string _baseUrl;
    private readonly string _responsibleEmployeeId;
    private readonly string _organizationId;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<BusinessRuClient> _logger;
    private readonly RateLimiter _rateLimiter;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly int _maxRetries;
    private readonly int _retryBaseDelayMs;

    private string _token = string.Empty;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public BusinessRuClient(
        HttpClient httpClient,
        string appId,
        string secret,
        string baseUrl,
        string responsibleEmployeeId,
        string organizationId,
        ILogger<BusinessRuClient> logger,
        RateLimiterOptions? rateLimiterOptions = null,
        int maxRetries = 3,
        int retryBaseDelayMs = 1000)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibleEmployeeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);

        _httpClient = httpClient;
        _appId = appId;
        _secret = secret;
        _baseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
        _responsibleEmployeeId = responsibleEmployeeId;
        _organizationId = organizationId;
        _logger = logger;
        _maxRetries = maxRetries;
        _retryBaseDelayMs = retryBaseDelayMs;
        _rateLimiter = new RateLimiter(rateLimiterOptions ?? new RateLimiterOptions(), logger);
        _jsonOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new BusinessRuDateTimeConverter()
            }
        };
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        _rateLimiter.Dispose();
    }
}
