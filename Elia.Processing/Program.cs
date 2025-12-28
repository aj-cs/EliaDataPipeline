using System;
using System.Threading.Tasks;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elia.Processing;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connectionString));

                services.AddTransient<ForecastProcessingService>();
                services.AddTransient<HistoricalPointProcessingService>();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Console.WriteLine("Applying migrations (processing)...");
        await db.Database.MigrateAsync();

        var forecastProcessor = scope.ServiceProvider.GetRequiredService<ForecastProcessingService>();
        var measurementProcessor = scope.ServiceProvider.GetRequiredService<HistoricalPointProcessingService>();

        try
        {
            await forecastProcessor.ProcessUnprocessedPvAsync();
            await forecastProcessor.ProcessUnprocessedWindAsync();

            await measurementProcessor.ProcessUnprocessedPvHistoricalAsync();
            await measurementProcessor.ProcessUnprocessedWindHistoricalAsync();

            Console.WriteLine("Processing successful.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during processing.");
            Console.WriteLine(ex);
        }

        Console.WriteLine("Finished processing run.");
    }
}

