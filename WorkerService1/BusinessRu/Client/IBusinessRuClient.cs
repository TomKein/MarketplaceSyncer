using WorkerService1.BusinessRu.Models.Responses;
using WorkerService1.BusinessRu.Query;

namespace WorkerService1.BusinessRu.Client;

public interface IBusinessRuClient : IDisposable
{
    Task<Good[]> GetGoodsAsync(
        int? businessId = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<int> CountGoodsAsync(
        bool includeArchived = false,
        int? type = 1,
        CancellationToken cancellationToken = default);

    Task<SalePriceListGoodPrice[]> GetGoodPricesAsync(
        string goodId,
        string? priceTypeId = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<SalePriceList[]> GetPriceListsAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<string> CreatePriceListAsync(
        string name,
        string priceTypeId,
        CancellationToken cancellationToken = default);

    Task<SalePriceType[]> GetPriceTypesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task UpdatePriceAsync(
        string priceId,
        decimal price,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string resource,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    Task<T> QueryAsync<T>(
        Dictionary<string, object> request,
        CancellationToken cancellationToken = default);

    IBusinessRuQuery CreateQuery();

    Task<TResponse> RequestAsync<TRequest, TResponse>(
        HttpMethod method,
        string model,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
}
