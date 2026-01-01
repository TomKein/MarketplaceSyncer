using WorkerService1;
using WorkerService1.Configuration;
using WorkerService1.Data;
using WorkerService1.Services;
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Http;
using FluentMigrator.Runner;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Options;

// Устанавливаем кодировку консоли для правильного отображения кириллицы
Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<BusinessRuOptions>(
    builder.Configuration.GetSection(BusinessRuOptions.SectionName));
builder.Services.Configure<PriceSyncOptions>(
    builder.Configuration.GetSection(PriceSyncOptions.SectionName));

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

builder.Services.AddSingleton<BusinessRuClient>(serviceProvider =>
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
        logger,
        rateLimiterOptions);
});

// Services
builder.Services.AddScoped<IPriceSyncService, PriceSyncService>();

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Run migrations
using (var scope = host.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

host.Run();