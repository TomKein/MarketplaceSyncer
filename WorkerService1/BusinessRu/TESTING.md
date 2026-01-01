# BusinessRu API Testing Guide

## Quick Start

### Run Tests (Recommended)
```powershell
# From WorkerService1 directory
.\test.ps1
```

### Manual Test Run
```powershell
# Stop any running instances
Get-Process -Name "WorkerService1" -ErrorAction SilentlyContinue | Stop-Process -Force

# Run in test mode
dotnet run --project WorkerService1.csproj -- --test
```

### Regular Service Run
```powershell
dotnet run --project WorkerService1.csproj
```

## What Gets Tested

The test mode (`--test` flag) runs the following tests automatically:

### 1. Count Goods
- Counts all active goods (archive=0, type=1)
- Uses: `CountGoodsAsync()` method
- Shows raw JSON response
- Logs: count result

### 2. Get Single Good
- Fetches first good from list
- Gets detailed info by ID
- Uses: `GetGoodByIdAsync(goodId)` method
- Shows raw JSON response
- Logs: good details (ID, Name, Part Number, etc.)

### 3. Get Good Prices
- Fetches first good
- Gets all prices for that good
- Uses: `GetGoodPricesAsync(goodId)` method
- Shows raw JSON response
- Logs: price entries with details

## Test Output

Each test displays:
1. **Test header** with separators
2. **Info logs** about what's being requested
3. **Raw JSON response** (formatted for readability)
4. **Parsed results** with key information
5. **Success/Error message**

Example output:
```
================================================================================
API TESTING MODE
================================================================================

--------------------------------------------------------------------------------
TEST: Count Goods
--------------------------------------------------------------------------------
info: WorkerService1.TestMode.ApiTester[0]
      Requesting goods count (archive=0, type=1)

RAW RESPONSE:
{
  "Count": 28118,
  "RequestCount": 0
}

info: WorkerService1.TestMode.ApiTester[0]
      Count result: 28118 goods found
SUCCESS: Found 28118 active goods
```

## Business Logic Methods

### Count Goods
```csharp
// Simple count with defaults
var count = await client.CountGoodsAsync(cancellationToken: ct);

// Count with parameters
var archivedCount = await client.CountGoodsAsync(
    includeArchived: true,
    type: 1,
    cancellationToken: ct);
```

### Get Single Good
```csharp
var good = await client.GetGoodByIdAsync("12345", ct);
Console.WriteLine($"Good: {good.Name}, Part: {good.PartNumber}");
```

### Get Good Prices
```csharp
var prices = await client.GetGoodPricesAsync("12345", limit: 10, ct);
foreach (var price in prices)
{
    Console.WriteLine($"Price: {price.Price}, Type: {price.PriceTypeId}");
}
```

## Logging Levels

Informative logs are automatically enabled in test mode:
- Request parameters
- Response summaries
- Success/Error messages
- Raw JSON responses in console

## Tips

1. **Quick Testing**: Use `.\test.ps1` script
2. **No Manual Cleanup**: Process stops automatically after tests
3. **Raw Responses**: All responses logged for model verification
4. **Informative Logs**: See exactly what's being sent/received

## Removing Test Mode

When testing is complete, remove test mode by:
1. Remove `--test` parameter check from Program.cs
2. Remove TestModeWorker.cs
3. Remove ApiTester.cs
4. Keep business logic methods in BusinessRuClient

## Notes

- Test mode runs once and exits automatically
- No need to manually stop processes
- All API calls use production credentials from appsettings
- Raw responses help verify/update models
