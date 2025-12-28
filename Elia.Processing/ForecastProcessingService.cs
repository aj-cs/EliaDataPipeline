using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Elia.Processing;

public class ForecastProcessingService
{
    private readonly AppDbContext _dbContext;

    public ForecastProcessingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // processes unprocessed RawData rows for the PV forecast dataset (ods087).
    public async Task ProcessUnprocessedPvAsync(int batchSize = 10)
    {
        const string source = "ods087-pv-forecast-near-real-time"; // must match ingestion

        var rawBatch = await _dbContext.RawData
            .Where(r => !r.Processed && r.Source == source)
            .OrderBy(r => r.FetchedAt)
            .Take(batchSize)
            .ToListAsync();

        if (!rawBatch.Any())
        {
            Console.WriteLine("No unprocessed RawData rows found for PV forecast.");
            return;
        }

        Console.WriteLine($"Found {rawBatch.Count} unprocessed RawData rows for source '{source}'.");

        foreach (var raw in rawBatch)
        {
            Console.WriteLine($"Processing RawData Id={raw.Id}, FetchedAt={raw.FetchedAt:O}");

            try
            {
                var createdCount = await ProcessSingleRawAsync(raw, datasetId: "ods087", isSolar: true);
                Console.WriteLine($"Parsed {createdCount} Forecast rows from RawData Id={raw.Id}.");

                // only mark as processed if we actually created rows, fixes bug
                if (createdCount > 0)
                {
                    raw.Processed = true;
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"Successfully processed RawData Id={raw.Id}.");
                }
                else
                {
                    Console.WriteLine($"Leaving RawData Id={raw.Id} as unprocessed because no forecasts were parsed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing RawData Id={raw.Id}: {ex.Message}");
                // processed =false
            }
        }
    }
    public async Task ProcessUnprocessedWindAsync(int batchSize = 10)
    {
        const string source = "ods086-wind-forecast-near-real-time";

        var rawBatch = await _dbContext.RawData
            .Where(r => !r.Processed && r.Source == source)
            .OrderBy(r => r.FetchedAt)
            .Take(batchSize)
            .ToListAsync();

        if (!rawBatch.Any())
        {
            Console.WriteLine("No unprocessed RawData rows found for Wind forecast.");
            return;
        }

        Console.WriteLine($"Found {rawBatch.Count} unprocessed RawData rows for source '{source}'.");

        foreach (var raw in rawBatch)
        {
            Console.WriteLine($"Processing Wind RawData Id={raw.Id}, FetchedAt={raw.FetchedAt:O}");

            try
            {
                var createdCount = await ProcessSingleRawAsync(
                    raw,
                    datasetId: "ods086",
                    isSolar: false);

                Console.WriteLine($"Parsed {createdCount} Wind Forecast rows from RawData Id={raw.Id}.");

                if (createdCount > 0)
                {
                    raw.Processed = true;
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"Successfully processed Wind RawData Id={raw.Id}.");
                }
                else
                {
                    Console.WriteLine($"Leaving Wind RawData Id={raw.Id} as unprocessed because no forecasts were parsed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing Wind RawData Id={raw.Id}: {ex.Message}");
            }
        }

    }

    /// parse a single RawData payload into Forecast rows, apply revision logic
    private async Task<int> ProcessSingleRawAsync(RawData raw, string datasetId, bool isSolar)
    {
        using var doc = JsonDocument.Parse(raw.Payload);
        var root = doc.RootElement;

        // { "total_count": ..., "results": [ { ... }, ... ] }
        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Cannot find 'results' array in payload.");
        }

        // parse all potential forecasts for this RawData row
        var newForecasts = results
            .EnumerateArray()
            .Select(record => isSolar
                    ? ParseSolarForecastRecord(record, raw, datasetId)
                    : ParseWindForecastRecord(record, raw, datasetId))
            .Where(f => f is not null)
            .Cast<Forecast>()
            .ToList();

        if (newForecasts.Count == 0)
        {
            Console.WriteLine($"No forecast rows parsed from RawData Id={raw.Id}.");
            return 0;
        }

        // apply revision logic per new forecast:
        // for each new (DatasetId, Region, ValidTime) we;
        //  1 mark existing non-revision rows as IsRevision = true
        //  2 insert the new row as IsRevision = false
        foreach (var nf in newForecasts)
        {
            // ensures new row starts as "current"
            nf.IsHistoricalVersion = false;

            var existingCurrents = await _dbContext.Forecasts
                .Where(f =>
                    f.DatasetId == nf.DatasetId &&
                    f.Region == nf.Region &&
                    f.ValidTime == nf.ValidTime &&
                    !f.IsHistoricalVersion)
                .ToListAsync();

            foreach (var prev in existingCurrents)
            {
                prev.IsHistoricalVersion = true;
            }

            await _dbContext.Forecasts.AddAsync(nf);
        }

        return newForecasts.Count;
    }

    // parse a single JSON record into a Forecast entity solar-only
    private static Forecast? ParseSolarForecastRecord(JsonElement record, RawData raw, string datasetId)
    {
        // VersionTime: ensure UTC
        DateTime versionTime = raw.FetchedAt.Kind == DateTimeKind.Utc
            ? raw.FetchedAt
            : raw.FetchedAt.ToUniversalTime();

        // datetime -> ValidTime (UTC)
        if (!record.TryGetProperty("datetime", out var dateTimeProp) ||
            dateTimeProp.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(dateTimeProp.GetString(), out var dto))
        {
            return null;
        }
        var validTime = dto.UtcDateTime;

        // region
        var region = "unknown";
        if (record.TryGetProperty("region", out var regionProp) &&
            regionProp.ValueKind == JsonValueKind.String)
        {
            region = regionProp.GetString() ?? "unknown";
        }

        // mostrecentforecast -> SolarMW
        if (!record.TryGetProperty("mostrecentforecast", out var mwProp) ||
            !mwProp.TryGetDouble(out var solarMw))
        {
            return null;
        }

        return new Forecast
        {
            DatasetId = datasetId,          // "ods087"
            Region = region,
            ValidTime = validTime,
            VersionTime = versionTime,
            SolarMW = solarMw,
            WindMW = null,
            Horizon = "mostrecent",
            IsHistoricalVersion = false,             // will be updated in revision logic
            RawDataId = raw.Id
        };
    }

    private static Forecast? ParseWindForecastRecord(JsonElement record, RawData raw, string datasetId)
    {
        DateTime versionTime = raw.FetchedAt.Kind == DateTimeKind.Utc
            ? raw.FetchedAt
            : raw.FetchedAt.ToUniversalTime();

        // datetime -> validtime utc
        if (!record.TryGetProperty("datetime", out var dtProp) ||
            dtProp.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(dtProp.GetString(), out var dto))
        {
            return null;
        }
        var validTime = dto.UtcDateTime;

        // region
        var region = "unknown";
        if (record.TryGetProperty("region", out var regionProp) &&
            regionProp.ValueKind == JsonValueKind.String)
        {
            region = regionProp.GetString() ?? "unknown";
        }

        // mostrecentforecast -> WindMW
        if (!record.TryGetProperty("mostrecentforecast", out var mw) ||
            !mw.TryGetDouble(out var windMw))
        {
            return null;
        }

        return new Forecast
        {
            DatasetId = datasetId,         // "ods086"
            Region = region,
            ValidTime = validTime,
            VersionTime = versionTime,
            SolarMW = null,
            WindMW = windMw,
            Horizon = "mostrecent",
            IsHistoricalVersion = false,
            RawDataId = raw.Id
        };
    }
}

