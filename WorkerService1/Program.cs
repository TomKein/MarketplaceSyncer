using WorkerService1;
using WorkerService1.Configuration;
using WorkerService1.Data;
using WorkerService1.Services;
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Http;
using FluentMigrator.Runner;
using Microsoft.Extensions.Options;
using WorkerService1.TestMode;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Parse command-line arguments
var isTestMode = args.Contains("--test");
var isProductionUpdate = true; // args.Contains("--update-all-prices");
var priceListId = "4258407"; //GetArgValue(args, "--price-list-id");
var startFromPage = 67; //GetArgValueInt(args, "--start-from-page");

string? GetArgValue(string[] arguments, string key)
{
    var index = Array.IndexOf(arguments, key);
    if (index >= 0 && index + 1 < arguments.Length)
        return arguments[index + 1];
    return null;
}

int? GetArgValueInt(string[] arguments, string key)
{
    var value = GetArgValue(arguments, key);
    if (int.TryParse(value, out var result))
        return result;
    return null;
}

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<BusinessRuOptions>(
    builder.Configuration.GetSection(BusinessRuOptions.SectionName));
builder.Services.Configure<PriceSyncOptions>(
    builder.Configuration.GetSection(PriceSyncOptions.SectionName));

builder.Services.Configure<PriceUpdateOptions>(
    builder.Configuration.GetSection("PriceUpdate"));

// Connection String
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Linq2Db
builder.Services.AddScoped<AppDbConnection>(provider => 
    new AppDbConnection(connectionString));

// FluentMigrator
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

// HttpClient + BusinessRuClient
builder.Services.AddHttpClient<BusinessRuClient>((serviceProvider, client) =>
{
    var businessOptions = serviceProvider.GetRequiredService<IOptions<BusinessRuOptions>>().Value;
    client.BaseAddress = new Uri(businessOptions.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
});

builder.Services.AddSingleton<IBusinessRuClient>(serviceProvider =>
{
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(BusinessRuClient));
    var businessOptions = serviceProvider.GetRequiredService<IOptions<BusinessRuOptions>>().Value;
    var logger = serviceProvider.GetRequiredService<ILogger<BusinessRuClient>>();
    
    var rateLimiterOptions = new RateLimiterOptions
    {
        MaxRequests = 500,
        TimeWindow = TimeSpan.FromMinutes(5),
        ThrottleThreshold = 0.8,
        MinThrottleDelay = TimeSpan.FromMilliseconds(businessOptions.RateLimitDelayMs)
    };

    return new BusinessRuClient(
        httpClient,
        businessOptions.AppId,
        businessOptions.Secret,
        businessOptions.BaseUrl,
        businessOptions.ResponsibleEmployeeId,
        businessOptions.OrganizationId,
        logger,
        rateLimiterOptions);
});

// Services
builder.Services.AddScoped<IPriceSyncService, PriceSyncService>();
builder.Services.AddScoped<IPriceUpdateService, PriceUpdateService>();
builder.Services.AddScoped<ProductionPriceUpdateRunner>();

// Register test mode services
builder.Services.AddSingleton<ApiTester>();
builder.Services.AddScoped<BatchPriceUpdateTester>();
builder.Services.AddScoped<PriceListSessionTester>();
builder.Services.AddScoped<ProductionUpdateTester>();

// Store CLI arguments for workers
builder.Services.AddSingleton(new ProductionUpdateArgs
{
    PriceListId = priceListId,
    StartFromPage = startFromPage
});

// Add hosted service based on mode
if (isTestMode)
{
    builder.Services.AddHostedService<TestModeWorker>();
}
else if (isProductionUpdate)
{
    builder.Services.AddHostedService<ProductionUpdateWorker>();
}
else
{
    builder.Services.AddHostedService<Worker>();
}

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
});

var host = builder.Build();

// Run migrations
using (var scope = host.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

host.Run();