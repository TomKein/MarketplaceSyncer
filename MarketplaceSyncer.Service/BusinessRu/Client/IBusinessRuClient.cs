using MarketplaceSyncer.Service.BusinessRu.Models.Responses;
using MarketplaceSyncer.Service.BusinessRu.Query;

namespace MarketplaceSyncer.Service.BusinessRu.Client;

public interface IBusinessRuClient : IDisposable
{
    Task<Good[]> GetGoodsAsync(
        long? businessId = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<int> CountGoodsAsync(
        bool includeArchived = false,
        int? type = 1,
        CancellationToken cancellationToken = default);

    Task<GroupResponse[]> GetGroupsAsync(CancellationToken cancellationToken = default);
    
    Task<UnitResponse[]> GetUnitsAsync(CancellationToken cancellationToken = default);

    Task<SalePriceListGoodPrice[]> GetGoodPricesAsync(
        long goodId,
        long? priceTypeId = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<SalePriceList[]> GetPriceListsAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<string> CreatePriceListAsync(
        string name,
        long priceTypeId,
        CancellationToken cancellationToken = default);

    Task<SalePriceType[]> GetPriceTypesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task UpdatePriceAsync(
        long priceId,
        decimal price,
        CancellationToken cancellationToken = default);

    Task<Good[]> GetGoodsChangedAfterAsync(
        DateTime since,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<Good[]> GetGoodsWithPriceChangesAfterAsync(
        DateTime since,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<GoodImageResponse[]> GetGoodImagesAsync(
        long goodId,
        CancellationToken cancellationToken = default);

    Task AddGoodImageAsync(
        long goodId,
        string name,
        string url,
        CancellationToken cancellationToken = default);

    Task<AttributeResponse[]> GetAttributesAsync(CancellationToken cancellationToken = default);

    Task<AttributeValueResponse[]> GetAttributeValuesAsync(
        long? attributeId = null,
        CancellationToken cancellationToken = default);

    Task<GoodAttributeResponse[]> GetGoodAttributesAsync(
        long? goodId = null,
        CancellationToken cancellationToken = default);

    Task<Store[]> GetStoresAsync(CancellationToken cancellationToken = default);

    Task<StoreGood[]> GetStoreGoodsAsync(
        long? storeId = null,
        CancellationToken cancellationToken = default);

    IBusinessRuQuery CreateQuery();

    Task<TResponse> RequestAsync<TRequest, TResponse>(
        HttpMethod method,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
}
