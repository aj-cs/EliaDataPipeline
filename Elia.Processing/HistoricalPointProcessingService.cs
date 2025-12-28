using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Elia.Processing;

public class HistoricalPointProcessingService
{
    private readonly AppDbContext _dbContext;

    public HistoricalPointProcessingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ProcessUnprocessedPvHistoricalAsync(int batchSize = 10)
    {
        const string source = "ods032-pv-historical";

        var rawBatch = await _dbContext.RawData
            .Where(r => !r.Processed && r.Source == source)
            .OrderBy(r => r.FetchedAt)
            .Take(batchSize)
            .ToListAsync();

        if (!rawBatch.Any())
        {
            Console.WriteLine("No unprocessed RawData rows found for PV historical.");
            return;
        }

        foreach (var raw in rawBatch)
        {
            Console.WriteLine($"Processing PV historical RawData Id={raw.Id}, FetchedAt={raw.FetchedAt:O}");

            try
            {
                var created = await ProcessSinglePvHistoricalRawAsync(raw, datasetId: "ods032");
                Console.WriteLine($"Parsed {created} PV measurements from RawData Id={raw.Id}.");

                if (created > 0)
                {
                    raw.Processed = true;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing PV historical RawData Id={raw.Id}: {ex.Message}");
            }
        }
    }

    public async Task ProcessUnprocessedWindHistoricalAsync(int batchSize = 10)
    {
        const string source = "ods031-wind-historical";

        var rawBatch = await _dbContext.RawData
            .Where(r => !r.Processed && r.Source == source)
            .OrderBy(r => r.FetchedAt)
            .Take(batchSize)
            .ToListAsync();

        if (!rawBatch.Any())
        {
            Console.WriteLine("No unprocessed RawData rows found for wind historical.");
            return;
        }

        foreach (var raw in rawBatch)
        {
            Console.WriteLine($"Processing wind historical RawData Id={raw.Id}, FetchedAt={raw.FetchedAt:O}");

            try
            {
                var created = await ProcessSingleWindHistoricalRawAsync(raw, datasetId: "ods031");
                Console.WriteLine($"Parsed {created} wind measurements from RawData Id={raw.Id}.");

                if (created > 0)
                {
                    raw.Processed = true;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing wind historical RawData Id={raw.Id}: {ex.Message}");
            }
        }
    }

    private async Task<int> ProcessSinglePvHistoricalRawAsync(RawData raw, string datasetId)
    {
        using var doc = JsonDocument.Parse(raw.Payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Cannot find 'results' array in payload.");
        }

        var list = results
            .EnumerateArray()
            .Select(r => ParsePvHistoricalRecord(r, raw, datasetId))
            .Where(m => m is not null)
            .Cast<HistoricalPoint>()
            .ToList();

        if (list.Count == 0) return 0;

        // Very simple upsert: avoid duplicates per key by checking existence.
        // This is OK at your current small ingestion batch sizes; later you can bulk upsert.
        foreach (var m in list)
        {
            var exists = await _dbContext.HistoricalPoints.AnyAsync(x =>
                x.DatasetId == m.DatasetId &&
                x.EnergyType == m.EnergyType &&
                x.Region == m.Region &&
                x.ValidTime == m.ValidTime &&
                x.OffshoreOnshore == null &&
                x.GridConnectionType == null);

            if (!exists)
            {
                await _dbContext.HistoricalPoints.AddAsync(m);
            }
        }

        await _dbContext.SaveChangesAsync();
        return list.Count;
    }

    private async Task<int> ProcessSingleWindHistoricalRawAsync(RawData raw, string datasetId)
    {
        using var doc = JsonDocument.Parse(raw.Payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Cannot find 'results' array in payload.");
        }

        var list = results
            .EnumerateArray()
            .Select(r => ParseWindHistoricalRecord(r, raw, datasetId))
            .Where(m => m is not null)
            .Cast<HistoricalPoint>()
            .ToList();

        if (list.Count == 0) return 0;

        foreach (var m in list)
        {
            var exists = await _dbContext.HistoricalPoints.AnyAsync(x =>
                x.DatasetId == m.DatasetId &&
                x.EnergyType == m.EnergyType &&
                x.Region == m.Region &&
                x.ValidTime == m.ValidTime &&
                x.OffshoreOnshore == m.OffshoreOnshore &&
                x.GridConnectionType == m.GridConnectionType);

            if (!exists)
            {
                await _dbContext.HistoricalPoints.AddAsync(m);
            }
        }

        await _dbContext.SaveChangesAsync();
        return list.Count;
    }

    private static HistoricalPoint? ParsePvHistoricalRecord(JsonElement record, RawData raw, string datasetId)
    {
        if (!record.TryGetProperty("datetime", out var dateTimeProp) ||
            dateTimeProp.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(dateTimeProp.GetString(), out var dto))
        {
            return null;
        }
        var validTime = dto.UtcDateTime;

        var region = "unknown";
        if (record.TryGetProperty("region", out var regionProp) &&
            regionProp.ValueKind == JsonValueKind.String)
        {
            region = regionProp.GetString() ?? "unknown";
        }

        if (!record.TryGetProperty("measured", out var measuredProp) ||
            !measuredProp.TryGetDouble(out var measuredMw))
        {
            return null;
        }

        return new HistoricalPoint
        {
            DatasetId = datasetId,
            EnergyType = "solar",
            Region = region,
            ValidTime = validTime,
            MeasuredMW = measuredMw,
            OffshoreOnshore = null,
            GridConnectionType = null,
            RawDataId = raw.Id
        };
    }

    private static HistoricalPoint? ParseWindHistoricalRecord(JsonElement record, RawData raw, string datasetId)
    {
        if (!record.TryGetProperty("datetime", out var dtProp) ||
            dtProp.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(dtProp.GetString(), out var dto))
        {
            return null;
        }
        var validTime = dto.UtcDateTime;

        var region = "unknown";
        if (record.TryGetProperty("region", out var regionProp) &&
            regionProp.ValueKind == JsonValueKind.String)
        {
            region = regionProp.GetString() ?? "unknown";
        }

        if (!record.TryGetProperty("measured", out var measuredProp) ||
            !measuredProp.TryGetDouble(out var measuredMw))
        {
            return null;
        }

        string? offshoreOnshore = null;
        if (record.TryGetProperty("offshoreonshore", out var ooProp) &&
            ooProp.ValueKind == JsonValueKind.String)
        {
            offshoreOnshore = ooProp.GetString();
        }

        string? gridConnectionType = null;
        if (record.TryGetProperty("gridconnectiontype", out var gcProp) &&
            gcProp.ValueKind == JsonValueKind.String)
        {
            gridConnectionType = gcProp.GetString();
        }

        return new HistoricalPoint
        {
            DatasetId = datasetId,
            EnergyType = "wind",
            Region = region,
            ValidTime = validTime,
            MeasuredMW = measuredMw,
            OffshoreOnshore = offshoreOnshore,
            GridConnectionType = gridConnectionType,
            RawDataId = raw.Id
        };
    }
}

