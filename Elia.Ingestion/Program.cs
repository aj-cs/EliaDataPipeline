using System;
using System.Threading.Tasks;
using Elia.Ingestion;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHttpClient<EliaForecastApiClient>(client =>
                {
                    client.BaseAddress = new Uri("https://opendata.elia.be");
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
        services.AddHttpClient<EliaHistoricalApiClient>(client =>
                {
                    client.BaseAddress = new Uri("https://opendata.elia.be");
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
        services.AddTransient<RawDataIngestionService>();

    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    Console.WriteLine("Applying migrations if applicable...");
    await db.Database.MigrateAsync();

    var ingestor = scope.ServiceProvider.GetRequiredService<RawDataIngestionService>();

    try
    {
        await ingestor.IngestPvForecastAsync();
        await ingestor.IngestWindForecastAsync();

        await ingestor.IngestPvHistoricalAsync();
        await ingestor.IngestWindHistoricalAsync();

        Console.WriteLine("Ingestion successful.");
    }

    catch (Exception ex)
    {
        Console.WriteLine("Error durign ingestion.");
        Console.WriteLine(ex);
    }

}

Console.WriteLine("Finished run.");


