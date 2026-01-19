using MarketplaceSyncer.Service.BusinessRu.Models.Requests;
using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.BusinessRu.Query;

namespace MarketplaceSyncer.Service.BusinessRu.Client;

/// <summary>
/// API-методы для работы с Business.ru.
/// </summary>
public sealed partial class BusinessRuClient
{
    public async Task<GoodResponse[]> GetGoodsAsync(
        long? businessId = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var allGoods = new List<GoodResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["type"] = "1",
                ["with_prices"] = "1",
                ["with_remains"] = "1",
                ["with_attributes"] = "1",
                ["with_additional_fields"] = "1",
                ["limit"] = "250",
                ["page"] = page.ToString()
            };

            if (businessId.HasValue)
                request["id"] = businessId.Value.ToString();

            if (!includeArchived)
                request["archive"] = "0";

            var result = await RequestAsync<Dictionary<string, string>, GoodResponse[]>(
                HttpMethod.Get, "goods", request, cancellationToken);

            if (result.Length == 0) break;
            
            allGoods.AddRange(result);
            if (result.Length < 250) break;
            
            page++;
        }

        return allGoods.ToArray();
    }

    public async Task<int> CountGoodsAsync(
        bool includeArchived = false,
        int? type = 1,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string> { ["count_only"] = "1" };

        if (!includeArchived)
            request["archive"] = "0";

        if (type.HasValue)
            request["type"] = type.Value.ToString();

        var response = await RequestAsync<Dictionary<string, string>, CountResponse>(
            HttpMethod.Get, "goods", request, cancellationToken);

        return response.Count;
    }

    public async Task<GroupResponse[]> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<GroupResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["limit"] = "250",
                ["page"] = page.ToString()
            };

            var result = await RequestAsync<Dictionary<string, string>, GroupResponse[]>(
                HttpMethod.Get, "groupsofgoods", request, cancellationToken);

            if (result.Length == 0) break;
            
            all.AddRange(result);
            if (result.Length < 250) break;
            
            page++;
        }
        
        return all.ToArray();
    }

    public async Task<UnitResponse[]> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<UnitResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["limit"] = "250",
                ["page"] = page.ToString()
            };

            var result = await RequestAsync<Dictionary<string, string>, UnitResponse[]>(
                HttpMethod.Get, "measures", request, cancellationToken);

            if (result.Length == 0) break;
            
            all.AddRange(result);
            if (result.Length < 250) break;
            
            page++;
        }
        
        return all.ToArray();
    }

    public async Task<SalePriceListGoodPrice[]> GetGoodPricesAsync(
        long goodId,
        long? priceTypeId = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        // Шаг 1: Получаем price list goods для данного товара
        var goodsRequest = new Dictionary<string, string> { ["good_id"] = goodId.ToString() };
        if (limit.HasValue)
            goodsRequest["limit"] = limit.Value.ToString();

        var priceListGoods = await RequestAsync<Dictionary<string, string>, SalePriceListGood[]>(
            HttpMethod.Get, "salepricelistgoods", goodsRequest, cancellationToken);

        if (priceListGoods.Length == 0)
            return [];

        // Шаг 2: Получаем цены для каждого price list good
        var allPrices = new List<SalePriceListGoodPrice>();

        foreach (var plg in priceListGoods)
        {
            var pricesRequest = new Dictionary<string, string> { ["price_list_good_id"] = plg.Id.ToString() };
            if (priceTypeId.HasValue)
                pricesRequest["price_type_id"] = priceTypeId.Value.ToString();

            var prices = await RequestAsync<Dictionary<string, string>, SalePriceListGoodPrice[]>(
                HttpMethod.Get, "salepricelistgoodprices", pricesRequest, cancellationToken);

            allPrices.AddRange(prices);
        }

        return allPrices.ToArray();
    }



    public async Task<SalePriceList[]> GetPriceListsAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>();
        if (limit.HasValue)
            request["limit"] = limit.Value.ToString();

        return await RequestAsync<Dictionary<string, string>, SalePriceList[]>(
            HttpMethod.Get, "salepricelists", request, cancellationToken);
    }

    public async Task<string> CreatePriceListAsync(
        string name,
        long priceTypeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var request = new Dictionary<string, string>
        {
            ["name"] = name,
            ["price_type_id"] = priceTypeId.ToString(),
            ["active"] = "1",
            ["responsible_employee_id"] = _responsibleEmployeeId,
            ["organization_id"] = _organizationId
        };

        var response = await RequestAsync<Dictionary<string, string>, SalePriceList>(
            HttpMethod.Post, "salepricelists", request, cancellationToken);

        _logger.LogInformation("Прайс-лист создан: {Id}", response.Id);
        return response.Id;
    }

    public async Task<SalePriceType[]> GetPriceTypesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>();
        if (limit.HasValue)
            request["limit"] = limit.Value.ToString();

        return await RequestAsync<Dictionary<string, string>, SalePriceType[]>(
            HttpMethod.Get, "salepricetypes", request, cancellationToken);
    }

    public async Task UpdatePriceAsync(
        long priceId,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>
        {
            ["id"] = priceId.ToString(),
            ["price"] = price.ToString("F2")
        };

        await RequestAsync<Dictionary<string, string>, object>(
            HttpMethod.Put, "salepricelistgoodprices", request, cancellationToken);
    }

    /// <summary>
    /// Получить товары, изменённые после указанной даты (инкрементальная синхронизация)
    /// </summary>
    public async Task<GoodResponse[]> GetGoodsChangedAfterAsync(
        DateTimeOffset since,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var allGoods = new List<GoodResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["type"] = "1",
                ["with_prices"] = "1",
                ["with_remains"] = "1",
                ["with_attributes"] = "1",
                ["with_additional_fields"] = "1",
                ["limit"] = "250",
                ["page"] = page.ToString(),
                ["updated[from]"] = since.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (!includeArchived)
                request["archive"] = "0";

            var result = await RequestAsync<Dictionary<string, string>, GoodResponse[]>(
                HttpMethod.Get, "goods", request, cancellationToken);

            if (result.Length == 0) break;

            allGoods.AddRange(result);
            if (result.Length < 250) break;

            page++;
        }

        return allGoods.ToArray();
    }

    /// <summary>
    /// Получить товары с изменёнными ценами/остатками после указанной даты
    /// </summary>
    public async Task<GoodResponse[]> GetGoodsWithPriceChangesAfterAsync(
        DateTimeOffset since,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var allGoods = new List<GoodResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["type"] = "1",
                ["with_prices"] = "1",
                ["with_remains"] = "1",
                ["with_attributes"] = "1",
                ["with_additional_fields"] = "1",
                ["limit"] = "250",
                ["page"] = page.ToString(),
                ["updated_remains_prices[from]"] = since.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (!includeArchived)
                request["archive"] = "0";

            var result = await RequestAsync<Dictionary<string, string>, GoodResponse[]>(
                HttpMethod.Get, "goods", request, cancellationToken);

            if (result.Length == 0) break;

            allGoods.AddRange(result);
            if (result.Length < 250) break;

            page++;
        }

        return allGoods.ToArray();
    }

    /// <summary>
    /// Получить изображения товара
    /// </summary>
    public async Task<GoodImageResponse[]> GetGoodImagesAsync(
        long goodId,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string> { ["good_id"] = goodId.ToString() };

        return await RequestAsync<Dictionary<string, string>, GoodImageResponse[]>(
            HttpMethod.Get, "goodsimages", request, cancellationToken);
    }

    /// <summary>
    /// Добавить изображение к товару
    /// </summary>
    public async Task AddGoodImageAsync(
        long goodId,
        string name,
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var request = new Dictionary<string, string>
        {
            ["good_id"] = goodId.ToString(),
            ["name"] = name ?? "image",
            ["url"] = url
        };

        await RequestAsync<Dictionary<string, string>, object>(
            HttpMethod.Post, "goodsimages", request, cancellationToken);
    }

    public async Task<AttributeResponse[]> GetAttributesAsync(CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>();
        return await RequestAsync<Dictionary<string, string>, AttributeResponse[]>(
            HttpMethod.Get, "attributesforgoods", request, cancellationToken);
    }

    public async Task<AttributeValueResponse[]> GetAttributeValuesAsync(
        long? attributeId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>();

        if (attributeId.HasValue)
            request["attribute_id"] = attributeId.Value.ToString();

        return await RequestAsync<Dictionary<string, string>, AttributeValueResponse[]>(
            HttpMethod.Get, "attributesforgoodsvalues", request, cancellationToken);
    }

    public async Task<GoodAttributeResponse[]> GetGoodAttributesAsync(
        long? goodId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string>();

        if (goodId.HasValue)
            request["good_id"] = goodId.Value.ToString();

        return await RequestAsync<Dictionary<string, string>, GoodAttributeResponse[]>(
            HttpMethod.Get, "goodsattributes", request, cancellationToken);
    }



    public async Task<StoreResponse[]> GetStoresAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<StoreResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["limit"] = "250",
                ["page"] = page.ToString()
            };

            var result = await RequestAsync<Dictionary<string, string>, StoreResponse[]>(
                HttpMethod.Get, "stores", request, cancellationToken);

            if (result.Length == 0) break;

            all.AddRange(result);
            if (result.Length < 250) break;

            page++;
        }

        return all.ToArray();
    }

    public async Task<StoreGoodResponse[]> GetStoreGoodsAsync(
        long? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var all = new List<StoreGoodResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["limit"] = "250",
                ["page"] = page.ToString()
            };

            if (storeId.HasValue)
                request["store_id"] = storeId.Value.ToString();

            var result = await RequestAsync<Dictionary<string, string>, StoreGoodResponse[]>(
                HttpMethod.Get, "storegoods", request, cancellationToken);

            if (result.Length == 0) break;

            all.AddRange(result);
            if (result.Length < 250) break;

            page++;
        }

        return all.ToArray();
    }

    public async Task<CommentResponse[]> GetCommentsAsync(
        string modelName = "goods",
        long? modelId = null,
        DateTimeOffset? from = null,
        CancellationToken cancellationToken = default)
    {
        var all = new List<CommentResponse>();
        int page = 1;

        while (true)
        {
            var request = new Dictionary<string, string>
            {
                ["limit"] = "250",
                ["page"] = page.ToString(),
                ["model_name"] = modelName
            };

            if (modelId.HasValue)
                request["document_id"] = modelId.Value.ToString();

            if (from.HasValue)
                request["date[from]"] = from.Value.ToString("dd.MM.yyyy HH:mm:ss");

            var result = await RequestAsync<Dictionary<string, string>, CommentResponse[]>(
                HttpMethod.Get, "comments", request, cancellationToken);

            if (result.Length == 0) break;

            all.AddRange(result);
            if (result.Length < 250) break;

            page++;
        }

        return all.ToArray();
    }

    public async Task<CommentResponse> CreateCommentAsync(
        CommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AuthorEmployeeId == null && long.TryParse(_responsibleEmployeeId, out var empId))
        {
            request.AuthorEmployeeId = empId;
        }
        
        var response = await RequestActionAsync(
            HttpMethod.Post, "comments", request, cancellationToken);
            
        return new CommentResponse(
            Id: response.Id,
            ModelName: request.ModelName,
            DocumentId: request.ModelId ?? 0, 
            AuthorEmployeeId: request.AuthorEmployeeId ?? 0, 
            TimeCreate: DateTimeOffset.Now, 
            Updated: null,
            Note: request.Note
        );
    }

    public async Task<CommentResponse> UpdateCommentAsync(
        CommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await RequestActionAsync(
            HttpMethod.Put, "comments", request, cancellationToken);

        return new CommentResponse(
            Id: request.Id ?? response.Id,
            ModelName: request.ModelName,
            DocumentId: request.ModelId ?? 0,
            AuthorEmployeeId: request.AuthorEmployeeId ?? 0,
            TimeCreate: null, // We don't know the original creation time here without fetching
            Updated: DateTimeOffset.Now,
            Note: request.Note
        );
    }

    public async Task DeleteCommentAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, string> { ["id"] = id.ToString() };
        // Delete usually returns status ok.
        await RequestActionAsync(
            HttpMethod.Delete, "comments", request, cancellationToken);
    }

    public IBusinessRuQuery CreateQuery() => new BusinessRuQuery();
}
