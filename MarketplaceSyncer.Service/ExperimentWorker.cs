using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.Data;
using MarketplaceSyncer.Service.Data.Models;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using LinqToDB.Async;
using System.Text.Json;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.BusinessRu.Models.Requests;

namespace MarketplaceSyncer.Service;

using Microsoft.Extensions.Hosting;

// ...

public class ExperimentWorker : BackgroundService
{
    private readonly IBusinessRuClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExperimentWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ExperimentWorker(
        IBusinessRuClient client, 
        IServiceScopeFactory scopeFactory,
        ILogger<ExperimentWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Экспериментальный воркер запущен.");

        try
        {
            // Эксперимент: Запрос типов цен продажи
            _logger.LogInformation("Запрашиваю типы цен продажи...");
            var priceTypes = await _client.GetPriceTypesAsync(cancellationToken: stoppingToken);
            _logger.LogInformation("Получено типов цен: {Count}", priceTypes.Length);
            
            foreach (var pt in priceTypes)
            {
                _logger.LogInformation("  Тип цены: Id={Id}, Name={Name}", pt.Id, pt.Name);
            }

            /* --- Остальные эксперименты временно отключены --- */
            
            // --- Experiment: Goods Comments ---
            _logger.LogInformation("Experiment: Goods Comments API Check...");

            // 1. Get specific good ID to comment on
            _logger.LogInformation("Fetching target good (Id=162695)...");
            var goods = await _client.GetGoodsAsync(businessId: 162695, cancellationToken: stoppingToken);
            if (goods.Length > 0)
            {
                var targetGoodId = goods[0].Id;
                _logger.LogInformation("Target Good ID: {Id}, Name: {Name}", targetGoodId, goods[0].Name);


                
            // 2. Create Comment
                var commentText = $"Тестовый комментарий от синхронизатора {DateTime.Now}";
                _logger.LogInformation("Creating comment: {Text}", commentText);
                
                CommentResponse createdComment = null;
                try 
                {
                    createdComment = await _client.CreateCommentAsync(new CommentRequest 
                    {
                        ModelId = targetGoodId,
                        ModelName = "goods",
                        Note = commentText
                    }, stoppingToken);
                    _logger.LogInformation("Comment Created! Id={Id}, Author={Author}", createdComment.Id, createdComment.AuthorEmployeeId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create comment.");
                }

                if (createdComment != null)
                {
                    // 3. Get Comments for this good
                    _logger.LogInformation("Fetching comments for good {Id}...", targetGoodId);
                    var comments = await _client.GetCommentsAsync(modelId: targetGoodId, cancellationToken: stoppingToken);
                    _logger.LogInformation("Found {Count} comments.", comments.Length);
                    foreach(var c in comments)
                    {
                         _logger.LogInformation(" - [{Id}] {Date}: {Note}", c.Id, c.TimeCreate, c.Note);
                    }

                    // 4. Update Comment
                    _logger.LogInformation("Updating comment {Id}...", createdComment.Id);
                    var updatedText = commentText + " (Updated)";
                    try
                    {
                        var updatedComment = await _client.UpdateCommentAsync(new CommentRequest
                        {
                            Id = createdComment.Id,
                            ModelId = targetGoodId,
                            Note = updatedText
                        }, stoppingToken);
                        _logger.LogInformation("Comment Updated! New content: {Note}", updatedComment.Note);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update comment.");
                    }

                    // 5. Delete Comment
                    _logger.LogInformation("Deleting comment {Id}...", createdComment.Id);
                    try
                    {
                        await _client.DeleteCommentAsync(createdComment.Id, stoppingToken);
                        _logger.LogInformation("Comment deleted successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete comment.");
                    }
                }
            }
            else
            {
                _logger.LogWarning("No goods found to test comments on.");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка эксперимента");
        }
        finally
        {
             _logger.LogInformation("Экспериментальный воркер завершил работу. Остановка приложения...");
             _lifetime.StopApplication();
        }
    }

    private async Task UpsertGroupAsync(AppDataConnection db, GroupResponse g, long? parentId, CancellationToken token)
    {
        // ... (unused for this experiment)
        await Task.CompletedTask;
    }
}
