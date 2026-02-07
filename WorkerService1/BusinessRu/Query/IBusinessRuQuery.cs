namespace WorkerService1.BusinessRu.Query;

public interface IBusinessRuQuery
{
    IBusinessRuQuery Resource(string resource);
    IBusinessRuQuery Fields(params string[] fields);
    IBusinessRuQuery Filter(string key, object value);
    IBusinessRuQuery Expand(params string[] relations);
    IBusinessRuQuery Limit(int limit);
    IBusinessRuQuery Offset(int offset);
    IBusinessRuQuery Page(int page);
    IBusinessRuQuery Count(bool enableCount = true);
    Dictionary<string, object> Build();
}
