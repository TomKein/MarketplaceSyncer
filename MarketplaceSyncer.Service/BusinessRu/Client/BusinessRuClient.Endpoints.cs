using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.BusinessRu.Query;

namespace MarketplaceSyncer.Service.BusinessRu.Client;

/// <summary>
/// API-методы для работы с Business.ru.
/// </summary>
public sealed partial class BusinessRuClient
{
    public async Task<Good[]> GetGoodsAsync(
        int? businessId = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var allGoods = new List<Good>();
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

            var result = await RequestAsync<Dictionary<string, string>, Good[]>(
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
        string goodId,
        string? priceTypeId = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goodId);

        // Шаг 1: Получаем price list goods для данного товара
        var goodsRequest = new Dictionary<string, string> { ["good_id"] = goodId };
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
            var pricesRequest = new Dictionary<string, string> { ["price_list_good_id"] = plg.Id };
            if (!string.IsNullOrWhiteSpace(priceTypeId))
                pricesRequest["price_type_id"] = priceTypeId;

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
        string priceTypeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(priceTypeId);

        var request = new Dictionary<string, string>
        {
            ["name"] = name,
            ["price_type_id"] = priceTypeId,
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
        string priceId,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(priceId);

        var request = new Dictionary<string, string>
        {
            ["id"] = priceId,
            ["price"] = price.ToString("F2")
        };

        await RequestAsync<Dictionary<string, string>, object>(
            HttpMethod.Put, "salepricelistgoodprices", request, cancellationToken);
    }

    /// <summary>
    /// Получить товары, изменённые после указанной даты (инкрементальная синхронизация)
    /// </summary>
    public async Task<Good[]> GetGoodsChangedAfterAsync(
        DateTime since,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var allGoods = new List<Good>();
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

            var result = await RequestAsync<Dictionary<string, string>, Good[]>(
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
    public async Task<Good[]> GetGoodsWithPriceChangesAfterAsync(
        DateTime since,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var allGoods = new List<Good>();
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

            var result = await RequestAsync<Dictionary<string, string>, Good[]>(
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
        string goodId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goodId);

        var request = new Dictionary<string, string> { ["good_id"] = goodId };

        return await RequestAsync<Dictionary<string, string>, GoodImageResponse[]>(
            HttpMethod.Get, "goodsimages", request, cancellationToken);
    }

    /// <summary>
    /// Добавить изображение к товару
    /// </summary>
    public async Task AddGoodImageAsync(
        string goodId,
        string name,
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goodId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var request = new Dictionary<string, string>
        {
            ["good_id"] = goodId,
            ["name"] = name ?? "image",
            ["url"] = url
        };

        await RequestAsync<Dictionary<string, string>, object>(
            HttpMethod.Post, "goodsimages", request, cancellationToken);
    }

    public IBusinessRuQuery CreateQuery() => new BusinessRuQuery();
}
