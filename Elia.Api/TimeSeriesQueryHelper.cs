using Elia.Api.Dtos;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Elia.Api.Helpers;

public static class TimeSeriesQueryHelper
{
    public static async Task<List<CombinedForecastDto>> QueryCombinedHistoricalPointAsync(
        AppDbContext db,
        string region,
        DateTime? from,
        DateTime? to)
    {
        var q = db.HistoricalPoints.AsNoTracking().Where(m => m.Region == region);

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            q = q.Where(m => m.ValidTime >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
            q = q.Where(m => m.ValidTime <= toUtc);
        }

        var rows = await q.Select(m => new
        {
            m.ValidTime,
            m.EnergyType,
            m.MeasuredMW
        }).ToListAsync();

        var combined = rows
            .GroupBy(x => x.ValidTime)
            .Select(g =>
            {
                var solar = g.Where(x => x.EnergyType == "solar").Sum(x => x.MeasuredMW);
                var wind = g.Where(x => x.EnergyType == "wind").Sum(x => x.MeasuredMW);

                double? solarOrNull = g.Any(x => x.EnergyType == "solar") ? solar : null;
                double? windOrNull = g.Any(x => x.EnergyType == "wind") ? wind : null;

                return new CombinedForecastDto(
                    ValidTimeUtc: g.Key,
                    SolarMW: solarOrNull,
                    WindMW: windOrNull,
                    TotalMW: (solarOrNull ?? 0.0) + (windOrNull ?? 0.0)
                );
            })
            .OrderBy(x => x.ValidTimeUtc)
            .ToList();

        return combined;
    }

    // Overlay rule: if measured exists at a timestamp, use it; otherwise use forecast.
    public static async Task<List<CombinedForecastDto>> QueryOverlayCombinedAsync(
        AppDbContext db,
        string region,
        DateTime? from,
        DateTime? to)
    {
        var forecast = await ForecastQueryHelper.QueryCombinedForecastAsync(
            db,
            region,
            from,
            to,
            includeHistoricalData: false);

        var hPoints = await QueryCombinedHistoricalPointAsync(db, region, from, to);

        var hPointsByTime = hPoints.ToDictionary(x => x.ValidTimeUtc, x => x);

        var times = forecast.Select(x => x.ValidTimeUtc)
            .Union(hPoints.Select(x => x.ValidTimeUtc))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var result = new List<CombinedForecastDto>(times.Count);

        foreach (var t in times)
        {
            if (hPointsByTime.TryGetValue(t, out var m))
            {
                result.Add(m);
            }
            else
            {
                var f = forecast.FirstOrDefault(x => x.ValidTimeUtc == t);
                if (f is not null)
                {
                    result.Add(f);
                }
            }
        }

        return result;
    }
}

