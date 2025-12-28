using System;
using System.Threading.Tasks;
using Elia.Shared.Models;
using Elia.Ingestion;
using Elia.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Azure runs this every 15 minutes.
//   1) Ingest new forecasts and historical into RawData
//   2) Process unprocessed RawData into Forecasts + HistoricalPoints
//   3) Exit

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;

        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Forecast host (near real-time datasets)
        services.AddHttpClient<EliaForecastApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://opendata.elia.be");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Historical host (external-elia datasets)
        services.AddHttpClient<EliaHistoricalApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://opendata.elia.be");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddTransient<RawDataIngestionService>();
        services.AddTransient<ForecastProcessingService>();
        services.AddTransient<HistoricalPointProcessingService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

var logger = services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("EliaWebJob");

try
{
    logger.LogInformation("Elia.WebJob starting at {Time}", DateTimeOffset.UtcNow);

    var db = services.GetRequiredService<AppDbContext>();
    logger.LogInformation("Applying migrations...");
    await db.Database.MigrateAsync();

    var ingestion = services.GetRequiredService<RawDataIngestionService>();
    var forecastProcessing = services.GetRequiredService<ForecastProcessingService>();
    var historicalProcessing = services.GetRequiredService<HistoricalPointProcessingService>();

    // ingestion
    logger.LogInformation("Starting ingestion cycle...");

    // Forecast ingestion (always)
    await ingestion.IngestPvForecastAsync();
    await ingestion.IngestWindForecastAsync();

    // Historical ingestion (safe for now since you’re using small limits;
    // later you’ll probably make this incremental/backfill in a separate job)
    await ingestion.IngestPvHistoricalAsync();
    await ingestion.IngestWindHistoricalAsync();

    logger.LogInformation("Ingestion cycle completed.");

    // processing
    logger.LogInformation("Starting processing cycle...");

    await forecastProcessing.ProcessUnprocessedPvAsync();
    await forecastProcessing.ProcessUnprocessedWindAsync();

    await historicalProcessing.ProcessUnprocessedPvHistoricalAsync();
    await historicalProcessing.ProcessUnprocessedWindHistoricalAsync();

    logger.LogInformation("Processing cycle completed.");
    logger.LogInformation("Elia.WebJob finished successfully at {Time}", DateTimeOffset.UtcNow);
}
catch (Exception ex)
{
    logger.LogError(ex, "Elia.WebJob failed with an unhandled exception.");
    Environment.ExitCode = -1;
}

