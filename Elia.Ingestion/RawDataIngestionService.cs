using System;
using System.Threading.Tasks;
using Elia.Shared.Models;

namespace Elia.Ingestion;

public class RawDataIngestionService
{
    private readonly AppDbContext _dbContext;
    private readonly EliaForecastApiClient _eliaForecastApiClient;
    private readonly EliaHistoricalApiClient _historicalClient;

    public RawDataIngestionService(AppDbContext dbContext, EliaForecastApiClient eliaForecast, EliaHistoricalApiClient historicalClient)
    {
        _dbContext = dbContext;
        _eliaForecastApiClient = eliaForecast;
        _historicalClient = historicalClient;
    }

    // Ingest PV (solar) forecast near real-time – ods087.
    public async Task IngestPvForecastAsync()
    {
        Console.WriteLine("Fetching PV Forecast data (ods087)...");
        var json = await _eliaForecastApiClient.GetPvForecastRawAsync(100);

        var raw = new RawData
        {
            FetchedAt = DateTime.UtcNow,
            Payload = json,
            Processed = false,
            Source = "ods087-pv-forecast-near-real-time"
        };

        _dbContext.RawData.Add(raw);
        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Saved PV forecast into RawData table.");
    }

    // Ingest Wind forecast near real-time – ods086.
    public async Task IngestWindForecastAsync()
    {
        Console.WriteLine("Fetching Wind Forecast data (ods086)...");
        var json = await _eliaForecastApiClient.GetWindForecastRawAsync(100);

        var raw = new RawData
        {
            FetchedAt = DateTime.UtcNow,
            Payload = json,
            Processed = false,
            Source = "ods086-wind-forecast-near-real-time"
        };

        _dbContext.RawData.Add(raw);
        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Saved Wind forecast into RawData table.");
    }

    public async Task IngestPvHistoricalAsync()
    {
        Console.WriteLine("Fetching PV Historical data (ods032)...");
        var json = await _historicalClient.GetPvHistoricalRawAsync(100);

        var raw = new RawData
        {
            FetchedAt = DateTime.UtcNow,
            Payload = json,
            Processed = false,
            Source = "ods032-pv-historical"
        };

        _dbContext.RawData.Add(raw);
        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Saved PV historical into RawData table.");
    }

    //  ods031 
    public async Task IngestWindHistoricalAsync()
    {
        Console.WriteLine("Fetching Wind Historical data (ods031)...");
        var json = await _historicalClient.GetWindHistoricalRawAsync(100);

        var raw = new RawData
        {
            FetchedAt = DateTime.UtcNow,
            Payload = json,
            Processed = false,
            Source = "ods031-wind-historical"
        };

        _dbContext.RawData.Add(raw);
        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Saved Wind historical into RawData table.");
    }
}

