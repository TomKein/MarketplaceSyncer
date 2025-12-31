namespace WorkerService1.Configuration;

/// <summary>
/// Настройки для подключения к Business.ru API
/// </summary>
public class BusinessRuOptions
{
    public const string SectionName = "BusinessRu";

    /// <summary>
    /// Базовый URL API Business.ru
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.business.ru/";

    /// <summary>
    /// ID организации в Business.ru
    /// </summary>
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>
    /// App ID для аутентификации
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Secret для аутентификации
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Максимальное количество записей на страницу
    /// </summary>
    public int PageLimit { get; set; } = 250;

    /// <summary>
    /// Задержка между запросами для соблюдения rate limit (мс)
    /// </summary>
    public int RateLimitDelayMs { get; set; } = 100;

    /// <summary>
    /// Максимальное количество попыток при ошибке
    /// </summary>
    public int MaxRetries { get; set; } = 5;
}