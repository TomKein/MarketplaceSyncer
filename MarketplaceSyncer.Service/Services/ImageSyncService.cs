using System.Security.Cryptography;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using LinqToDB;
using LinqToDB.Async;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Сервис для синхронизации изображений товаров
/// </summary>
public class ImageSyncService
{
    private readonly IBusinessRuClient _client;
    private readonly AppDataConnection _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageSyncService> _logger;

    public ImageSyncService(
        IBusinessRuClient client,
        AppDataConnection db,
        HttpClient httpClient,
        ILogger<ImageSyncService> logger)
    {
        _client = client;
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Синхронизировать изображения для товара
    /// </summary>
    public async Task SyncGoodImagesAsync(int goodId, string businessRuGoodId, CancellationToken ct = default)
    {
        try
        {
            // Получаем изображения из Business.ru
            var apiImages = await _client.GetGoodImagesAsync(businessRuGoodId, ct);
            
            if (apiImages.Length == 0)
            {
                _logger.LogDebug("Товар {GoodId} не имеет изображений", goodId);
                return;
            }

            // Получаем существующие изображения из БД
            var existingImages = await _db.GoodImages
                .Where(i => i.GoodId == goodId)
                .ToListAsync(ct);

            var position = 0;
            foreach (var apiImage in apiImages)
            {
                if (string.IsNullOrEmpty(apiImage.Url))
                    continue;

                await DownloadAndSaveImageAsync(goodId, apiImage, position++, existingImages, ct);
            }

            // Удаляем изображения, которых больше нет в API
            var apiUrls = apiImages.Select(i => i.Url).ToHashSet();
            var toDelete = existingImages.Where(e => !apiUrls.Contains(e.Url)).ToList();
            
            foreach (var img in toDelete)
            {
                await _db.GoodImages.DeleteAsync(i => i.Id == img.Id, ct);
                _logger.LogInformation("Удалено изображение {Id} для товара {GoodId}", img.Id, goodId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка синхронизации изображений для товара {GoodId}", goodId);
            throw;
        }
    }

    /// <summary>
    /// Скачать изображение и сохранить в БД
    /// </summary>
    public async Task<GoodImage?> DownloadAndSaveImageAsync(
        int goodId,
        GoodImageResponse apiImage,
        int position,
        List<GoodImage>? existingImages = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(apiImage.Url))
            return null;

        try
        {
            // Скачиваем изображение
            var imageData = await DownloadImageAsync(apiImage.Url, ct);
            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogWarning("Не удалось скачать изображение: {Url}", apiImage.Url);
                return null;
            }

            // Вычисляем MD5 хеш
            var hash = ComputeMd5Hash(imageData);

            // Определяем content type
            var contentType = DetectContentType(imageData);

            // Проверяем, существует ли уже такое изображение
            var existing = existingImages?.FirstOrDefault(e => e.Url == apiImage.Url);
            
            if (existing != null)
            {
                // Если хеш изменился — обновляем
                if (existing.Hash != hash)
                {
                    existing.Data = imageData;
                    existing.Hash = hash;
                    existing.ContentType = contentType;
                    existing.Position = position;
                    existing.DownloadedAt = DateTime.UtcNow;

                    await _db.UpdateAsync(existing, token: ct);
                    _logger.LogInformation("Обновлено изображение {Id} для товара {GoodId} (hash изменился)", 
                        existing.Id, goodId);
                }
                return existing;
            }

            // Создаём новую запись
            var newImage = new GoodImage
            {
                GoodId = goodId,
                Name = apiImage.Name,
                Url = apiImage.Url,
                Data = imageData,
                ContentType = contentType,
                Hash = hash,
                Position = position,
                DownloadedAt = DateTime.UtcNow
            };

            newImage.Id = await _db.InsertWithInt64IdentityAsync(newImage, token: ct);
            _logger.LogInformation("Добавлено изображение {Id} для товара {GoodId}: {Url}", 
                newImage.Id, goodId, apiImage.Url);

            return newImage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки изображения {Url}", apiImage.Url);
            return null;
        }
    }

    /// <summary>
    /// Скачать данные изображения по URL
    /// </summary>
    public async Task<byte[]?> DownloadImageAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP ошибка при загрузке {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Вычислить MD5 хеш данных
    /// </summary>
    public static string ComputeMd5Hash(byte[] data)
    {
        var hashBytes = MD5.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Определить MIME-тип по magic bytes
    /// </summary>
    public static string DetectContentType(byte[] data)
    {
        if (data.Length < 4)
            return "application/octet-stream";

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";

        // GIF: 47 49 46
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return "image/gif";

        // WebP: 52 49 46 46 ... 57 45 42 50
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && 
            data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";

        return "image/jpeg"; // default
    }
}
