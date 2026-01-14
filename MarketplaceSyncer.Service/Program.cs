using MarketplaceSyncer.Service;
using MarketplaceSyncer.Service.BusinessRu.Client;
using MarketplaceSyncer.Service.BusinessRu.Http;
using MarketplaceSyncer.Service.Configuration;
using MarketplaceSyncer.Service.Data;
using FluentMigrator.Runner;
using LinqToDB;
using LinqToDB.Extensions.DependencyInjection;
using LinqToDB.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Configuration
builder.Services.Configure<BusinessRuOptions>(
    builder.Configuration.GetSection(BusinessRuOptions.SectionName));
builder.Services.Configure<SynchronizationOptions>(
    builder.Configuration.GetSection("Synchronization"));

// Services
builder.Services.AddHttpClient(nameof(BusinessRuClient));

// Database & Migrations
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

// Linq2Db
builder.Services.AddLinqToDBContext<AppDataConnection>((provider, options) =>
    options
        .UsePostgreSQL(connectionString!)
        .UseDefaultLogging(provider));

builder.Services.AddTransient<IBusinessRuClient>(sp => 
{
    var options = sp.GetRequiredService<IOptions<BusinessRuOptions>>().Value;
    
    // Validate critical options
    if (string.IsNullOrWhiteSpace(options.AppId))
    {
        // Allow starting without config for initial setup, but warn or fail early if needed.
        // For now, let's assume it might not be configured yet.
    }

    var httpClient = sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(BusinessRuClient));
    
    var logger = sp.GetRequiredService<ILogger<BusinessRuClient>>();
    
    var rateLimiterOptions = new RateLimiterOptions
    {
        MaxRequests = options.RateLimitRequestCount,
        TimeWindow = TimeSpan.FromMilliseconds(options.RateLimitTimeWindowMs)
    };

    return new BusinessRuClient(
        httpClient,
        options.AppId ?? "",
        options.Secret ?? "",
        options.BaseUrl ?? "https://api.business.ru",
        options.ResponsibleEmployeeId ?? "",
        options.OrganizationId ?? "",
        logger,
        rateLimiterOptions
    );
});

// Workers
// Раскомментируйте нужный сервис
// builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ExperimentWorker>();

var host = builder.Build();
MigrationExtensions.EnsureDatabase(connectionString ?? throw new InvalidOperationException("Connection string not found"));
host.MigrateDatabase();
host.Run();
