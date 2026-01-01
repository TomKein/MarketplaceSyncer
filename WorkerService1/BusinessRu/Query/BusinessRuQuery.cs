namespace WorkerService1.BusinessRu.Query;

public sealed class BusinessRuQuery : IBusinessRuQuery
{
    private readonly Dictionary<string, object> _query = new();
    private readonly Dictionary<string, object> _filters = new();
    private readonly List<string> _fields = new();
    private readonly List<string> _expand = new();

    public IBusinessRuQuery Resource(string resource)
    {
        _query["resource"] = resource;
        return this;
    }

    public IBusinessRuQuery Fields(params string[] fields)
    {
        _fields.AddRange(fields);
        return this;
    }

    public IBusinessRuQuery Filter(string key, object value)
    {
        _filters[key] = value;
        return this;
    }

    public IBusinessRuQuery Expand(params string[] relations)
    {
        _expand.AddRange(relations);
        return this;
    }

    public IBusinessRuQuery Limit(int limit)
    {
        _query["limit"] = limit;
        return this;
    }

    public IBusinessRuQuery Offset(int offset)
    {
        _query["offset"] = offset;
        return this;
    }

    public IBusinessRuQuery Page(int page)
    {
        _query["page"] = page;
        return this;
    }

    public IBusinessRuQuery Count(bool enableCount = true)
    {
        if (enableCount)
            _query["count"] = 1;
        else
            _query.Remove("count");
        
        return this;
    }

    public Dictionary<string, object> Build()
    {
        if (_fields.Count > 0)
            _query["fields"] = _fields.ToArray();
        
        if (_filters.Count > 0)
            _query["filter"] = _filters;
        
        if (_expand.Count > 0)
            _query["expand"] = _expand.ToArray();

        return _query;
    }
}
