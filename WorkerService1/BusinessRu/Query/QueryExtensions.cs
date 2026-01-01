using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Models.Responses;

namespace WorkerService1.BusinessRu.Query;

public static class QueryExtensions
{
    public static async Task<int> ExecuteCountAsync(
        this IBusinessRuQuery query,
        IBusinessRuClient client,
        CancellationToken cancellationToken = default)
    {
        var request = query.Count().Build();
        var response = await client.QueryAsync<CountResponse>(
            request,
            cancellationToken);
        
        return response.Count;
    }

    public static async Task<T> ExecuteAsync<T>(
        this IBusinessRuQuery query,
        IBusinessRuClient client,
        CancellationToken cancellationToken = default)
    {
        var request = query.Build();
        return await client.QueryAsync<T>(request, cancellationToken);
    }
}
