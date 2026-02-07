using System.Security.Cryptography;
using LinqToDB;
using LinqToDB.Async;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Services;

/// <summary>
/// Сервис для синхронизации изображений товаров
/// </summary>
public class ImageSyncService(
    IBusinessRuClient client,
    AppDataConnection db,
    HttpClient httpClient,
    ILogger<ImageSyncService> logger)
{
    /// <summary>
    /// Синхронизировать изображения для товара
    /// </summary>
    /// <summary>
    /// Синхронизировать изображения для товара
    /// </summary>
    public async Task SyncGoodImagesAsync(long goodId, long businessRuGoodId, CancellationToken ct = default)
    {
        try
        {
            // Получаем изображения из Business.ru
            var apiImages = await client.GetGoodImagesAsync(businessRuGoodId, ct);
            
            if (apiImages.Length == 0)
            {
                logger.LogDebug("Товар {GoodId} не имеет изображений", goodId);
                return;
            }

            // Получаем существующие изображения из БД
            var existingImages = await db.GoodImages
                .Where(i => i.GoodId == goodId)
                .ToListAsync(ct);

            foreach (var apiImage in apiImages)
            {
                if (string.IsNullOrEmpty(apiImage.Url))
                    continue;

                await DownloadAndSaveImageAsync(goodId, apiImage, existingImages, ct);
            }

            // Удаляем изображения, которых больше нет в API
            var apiUrls = apiImages.Select(i => i.Url).ToHashSet();
            var toDelete = existingImages.Where(e => !apiUrls.Contains(e.Url)).ToList();
            
            foreach (var img in toDelete)
            {
                await db.GoodImages.DeleteAsync(i => i.Id == img.Id, ct);
                logger.LogInformation("Удалено изображение {Id} для товара {GoodId}", img.Id, goodId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка синхронизации изображений для товара {GoodId}", goodId);
            throw;
        }
    }

    /// <summary>
    /// Скачать изображение и сохранить в БД
    /// </summary>
    public async Task<GoodImage?> DownloadAndSaveImageAsync(
        long goodId,
        GoodImageResponse apiImage,
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
                logger.LogWarning("Не удалось скачать изображение: {Url}", apiImage.Url);
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
                // Если хеш или метаданные изменились — обновляем
                if (existing.Hash != hash || existing.Sort != (apiImage.Sort ?? 0))
                {
                    existing.Data = imageData;
                    existing.Hash = hash;
                    existing.ContentType = contentType;
                    existing.Sort = apiImage.Sort ?? 0;
                    existing.TimeCreate = apiImage.TimeCreate;
                    existing.DownloadedAt = DateTimeOffset.UtcNow;

                    await db.UpdateAsync(existing, token: ct);
                    logger.LogInformation("Обновлено изображение {Id} для товара {GoodId} (hash/sort изменился)", 
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
                Sort = apiImage.Sort ?? 0,
                TimeCreate = apiImage.TimeCreate,
                DownloadedAt = DateTimeOffset.UtcNow
            };

            newImage.Id = await db.InsertWithInt64IdentityAsync(newImage, token: ct);
            logger.LogInformation("Добавлено изображение {Id} для товара {GoodId}: {Url}", 
                newImage.Id, goodId, apiImage.Url);

            return newImage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка загрузки изображения {Url}", apiImage.Url);
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
            using var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP ошибка при загрузке {Url}", url);
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
