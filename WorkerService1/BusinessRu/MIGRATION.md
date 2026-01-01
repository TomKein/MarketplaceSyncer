# Migration Guide: BusinessRu API Client Refactoring

## Overview

The BusinessRu API client has been refactored from a monolithic structure 
to a modular, flexible architecture supporting multiple query approaches.

## What Changed

### Old Structure (Removed)
```
WorkerService1/
├── BusinessRuClient.cs       (monolithic client)
├── BusinessRuModels.cs       (all models in one file)
├── RateLimiter.cs
├── RateLimiterOptions.cs
└── PhpKSort.cs
```

### New Structure
```
WorkerService1/BusinessRu/
├── Client/
│   ├── IBusinessRuClient.cs
│   └── BusinessRuClient.cs
├── Query/
│   ├── IBusinessRuQuery.cs
│   ├── BusinessRuQuery.cs
│   └── QueryExtensions.cs
├── Models/
│   ├── Requests/
│   │   ├── GetGoodsRequest.cs
│   │   ├── GetSalePriceListsRequest.cs
│   │   └── UpdatePriceRequest.cs
│   ├── Responses/
│   │   ├── ApiResponse.cs
│   │   ├── CountResponse.cs
│   │   ├── Good.cs
│   │   ├── SalePriceList.cs
│   │   └── SalePriceType.cs
│   └── Common/
│       └── EntityResponse.cs
└── Http/
    ├── RateLimiter.cs
    ├── RateLimiterOptions.cs
    └── PhpKSort.cs
```

## Key Improvements

1. **Separation of Concerns** - Each file has a single responsibility
2. **Flexible Query API** - Three ways to query:
   - Typed methods (simple cases)
   - Dictionary queries (complex cases)
   - Fluent builder (readable code)
3. **Count Queries** - Efficient record counting without fetching data
4. **Better Testability** - All components are interface-based
5. **Line Length** - All lines are max 128 characters
6. **No Emojis** - Clean logs and comments

## Breaking Changes

### Namespace Changes
```csharp
// Old
using BusinessRu.ApiClient;

// New
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Models.Responses;
using WorkerService1.BusinessRu.Models.Requests;
```

### Type Name Conflicts
When using both DB models and API models, use aliases:
```csharp
using ApiGood = WorkerService1.BusinessRu.Models.Responses.Good;
using DbGood = WorkerService1.Data.Models.Good;
```

### API Method Changes

#### Old API (still supported for backward compatibility)
```csharp
var goods = await client.RequestAsync<GetGoodsRequest, Good[]>(
    HttpMethod.Get,
    "goods",
    new GetGoodsRequest(type: 1, archive: 0),
    cancellationToken);
```

#### New API - Typed Methods (recommended for simple cases)
```csharp
var goods = await client.GetGoodsAsync(
    businessId: 123,
    includeArchived: false,
    cancellationToken);
```

#### New API - Flexible Query (for complex cases)
```csharp
var request = new Dictionary<string, object>
{
    ["resource"] = "good",
    ["fields"] = new[] { "id", "name" },
    ["filter"] = new Dictionary<string, object>
    {
        ["business_id"] = 123,
        ["active"] = true
    },
    ["expand"] = new[] { "prices" },
    ["limit"] = 100
};

var response = await client.QueryAsync<ApiResponse<Good[]>>(
    request,
    cancellationToken);
```

#### New API - Fluent Builder (for readability)
```csharp
var query = client.CreateQuery()
    .Resource("good")
    .Fields("id", "name")
    .Filter("business_id", 123)
    .Expand("prices")
    .Limit(100)
    .Build();

var response = await client.QueryAsync<ApiResponse<Good[]>>(
    query,
    cancellationToken);
```

## Migration Checklist

- [x] Update namespace imports
- [x] Resolve type name conflicts with aliases
- [x] Update client instantiation (if custom)
- [x] Consider using new typed methods for common operations
- [x] Consider using flexible queries for complex scenarios
- [x] Test all API calls
- [x] Remove old file references

## Benefits of New Approach

### No Extra Allocations
Dictionary-based queries avoid creating intermediate request objects:
```csharp
// Direct dictionary - no extra allocations
var query = new Dictionary<string, object> { ... };
await client.QueryAsync<T>(query, ct);
```

### Dynamic Filtering
Easy to build queries with conditional filters:
```csharp
var filters = new Dictionary<string, object>();
if (businessId.HasValue)
    filters["business_id"] = businessId.Value;
if (!includeArchived)
    filters["archive"] = false;

query["filter"] = filters;
```

### Count Queries
Efficient counting without fetching data:
```csharp
var count = await client.CountAsync(
    "good",
    filters: new Dictionary<string, object> 
    { 
        ["active"] = true 
    },
    ct);
```

## Support

For questions or issues, refer to:
- `WorkerService1/BusinessRu/README.md` - Usage examples
- API documentation: https://api-online.class365.ru/

## Code Quality Standards

All code follows WorkerService1 standards:
- Max line length: 128 characters
- No emojis in logs/comments
- Proper separation of concerns
- Interface-based design
- Modern C# patterns
