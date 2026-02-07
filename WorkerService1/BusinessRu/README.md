# BusinessRu API Client

Flexible and efficient client for Business.ru API with support for dynamic queries.

## Architecture

```
BusinessRu/
├── Client/          # API client implementation
├── Query/           # Fluent query builder
├── Models/          # Request/Response models
│   ├── Requests/    # Request DTOs
│   ├── Responses/   # Response DTOs
│   └── Common/      # Shared models
└── Http/            # HTTP utilities (rate limiting, sorting)
```

## Features

- **Typed Methods** - Convenient methods for common operations
- **Flexible Queries** - Dictionary-based requests for complex scenarios
- **Fluent API** - Query builder for readable code
- **Count Queries** - Efficient record counting
- **Rate Limiting** - Automatic throttling and retry logic
- **Authentication** - Automatic token management

## Usage Examples

### 1. Typed Methods (Simple)

```csharp
// Get all active goods
var goods = await client.GetGoodsAsync(
    businessId: 123,
    includeArchived: false,
    cancellationToken);

// Get price lists
var priceLists = await client.GetPriceListsAsync(
    limit: 100,
    cancellationToken);

// Update price
await client.UpdatePriceAsync(
    priceId: "12345",
    price: 999.99m,
    cancellationToken);
```

### 2. Flexible Queries (Complex)

```csharp
// Complex query with filters and expansion
var request = new Dictionary<string, object>
{
    ["resource"] = "good",
    ["fields"] = new[] { "id", "name", "prices" },
    ["filter"] = new Dictionary<string, object>
    {
        ["business_id"] = 123,
        ["active"] = true,
        ["category_id"] = new[] { 1, 2, 3 }
    },
    ["expand"] = new[] { "prices", "categories" },
    ["limit"] = 100,
    ["offset"] = 0
};

var response = await client.QueryAsync<ApiResponse<Good[]>>(
    request,
    cancellationToken);

var goods = response.Result;
```

### 3. Fluent Builder (Readable)

```csharp
// Using query builder
var query = client.CreateQuery()
    .Resource("good")
    .Fields("id", "name", "prices")
    .Filter("business_id", 123)
    .Filter("active", true)
    .Expand("prices", "categories")
    .Limit(100)
    .Build();

var response = await client.QueryAsync<ApiResponse<Good[]>>(
    query,
    cancellationToken);
```

### 4. Count Queries

```csharp
// Simple count
var totalGoods = await client.CountAsync(
    "good",
    cancellationToken: ct);

// Count with filters
var activeGoods = await client.CountAsync(
    "good",
    filters: new Dictionary<string, object>
    {
        ["business_id"] = 123,
        ["archive"] = false
    },
    cancellationToken: ct);

// Using fluent builder
var count = await client.CreateQuery()
    .Resource("good")
    .Filter("active", true)
    .ExecuteCountAsync(client, ct);
```

### 5. Extension Methods

```csharp
// Execute query with extension method
var goods = await client.CreateQuery()
    .Resource("good")
    .Fields("id", "name")
    .Filter("business_id", 123)
    .ExecuteAsync<ApiResponse<Good[]>>(client, ct);
```

## Configuration

```csharp
services.AddHttpClient<IBusinessRuClient, BusinessRuClient>((sp, client) =>
{
    client.BaseAddress = new Uri("https://api.business.ru/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
});

services.AddSingleton<IBusinessRuClient>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(BusinessRuClient));
    
    var logger = sp.GetRequiredService<ILogger<BusinessRuClient>>();
    var options = sp.GetRequiredService<IOptions<BusinessRuOptions>>().Value;
    
    return new BusinessRuClient(
        httpClient,
        options.AppId,
        options.Secret,
        options.BaseUrl,
        logger,
        new RateLimiterOptions
        {
            MaxRequests = 500,
            TimeWindow = TimeSpan.FromSeconds(60)
        });
});
```

## Best Practices

1. **Use typed methods** for common operations (better performance)
2. **Use flexible queries** for complex scenarios with multiple filters
3. **Use fluent builder** for improved code readability
4. **Always handle** `ApiResponse<T>` wrapper in responses
5. **Check for null** in `Result` property
6. **Use count queries** instead of fetching all records when only count is needed

## Notes

- All methods support cancellation tokens
- Rate limiting is handled automatically
- Token refresh is automatic
- Retries are built-in (max 3 attempts)
- No emojis in logs (following project standards)
