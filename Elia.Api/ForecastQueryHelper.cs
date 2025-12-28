using System.Globalization;
using System.Text;
using Elia.Api.Dtos;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Elia.Api.Helpers;

public static class ForecastQueryHelper
{
    public static async Task<List<CombinedForecastDto>> QueryCombinedForecastAsync(
        AppDbContext db,
        string region,
        DateTime? from,
        DateTime? to,
        bool includeHistoricalData)
    {
        var query = db.Forecasts
            .AsNoTracking()
            .Where(f => f.Region == region);

        if (!includeHistoricalData)
        {
            query = query.Where(f => !f.IsHistoricalVersion);
        }

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query = query.Where(f => f.ValidTime >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
            query = query.Where(f => f.ValidTime <= toUtc);
        }

        var rows = await query
            .Select(f => new
            {
                f.ValidTime,
                f.SolarMW,
                f.WindMW
            })
            .ToListAsync();

        var combined = rows
            .GroupBy(x => x.ValidTime)
            .Select(g =>
            {
                var solar = g.Where(x => x.SolarMW.HasValue).Sum(x => x.SolarMW!.Value);
                var wind = g.Where(x => x.WindMW.HasValue).Sum(x => x.WindMW!.Value);

                double? solarOrNull = g.Any(x => x.SolarMW.HasValue) ? solar : null;
                double? windOrNull = g.Any(x => x.WindMW.HasValue) ? wind : null;

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
}

