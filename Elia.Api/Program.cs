using System.Globalization;
using System.Text;
using Elia.Api.Dtos;
using Elia.Api.Helpers;
using Elia.Shared.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// If you later need CORS for other frontends (different origin), you can re-enable this.
//builder.Services.AddCors(options =>
//{
//    options.AddDefaultPolicy(policy =>
//    {
//        policy
//            .AllowAnyOrigin()
//            .AllowAnyHeader()
//            .AllowAnyMethod();
//    });
//});

var app = builder.Build();

// Order of middleware:
// 1. HTTPS redirect
// 2. DefaultFiles + StaticFiles (serve wwwroot, including index.html)
app.UseHttpsRedirection();
app.UseDefaultFiles();   // looks for index.html in wwwroot
app.UseStaticFiles();

// app.UseCors(); // enable if needed

// NOTE: Removed the old root endpoint that returned "Elia API is running."
// The root path ("/") will now serve wwwroot/index.html via UseDefaultFiles/UseStaticFiles.

// -------------------- API ENDPOINTS --------------------

// main endpoint : solar + wind per region
// GET /api/forecasts/combined?region=Flanders&from=2025-12-02&to=2025-12-05&includeHistoricalData=false
app.MapGet("/api/forecasts/combined", async (
    string region,
    DateTime? from,
    DateTime? to,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var includeHistorical = includeHistoricalData ?? false;

    // Normalize date filters to UTC (same as before)
    DateTime? fromUtc = null;
    DateTime? toUtc = null;

    if (from.HasValue)
    {
        fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
    }

    if (to.HasValue)
    {
        toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
    }

    // 1) Standard behavior: includeHistoricalData = false -> one latest value per ValidTime 
    if (!includeHistorical)
    {
        var series = await ForecastQueryHelper.QueryCombinedForecastAsync(
            db,
            region,
            fromUtc,
            toUtc,
            includeHistoricalData: false);

        return Results.Ok(series);
    }

    // includeHistoricalData = true -> return all versions per ValidTime, no summing
    var query = db.Forecasts
        .AsNoTracking()
        .Where(f => f.Region == region);

    if (fromUtc.HasValue)
    {
        query = query.Where(f => f.ValidTime >= fromUtc.Value);
    }

    if (toUtc.HasValue)
    {
        query = query.Where(f => f.ValidTime <= toUtc.Value);
    }

    var rows = await query
        .OrderBy(f => f.ValidTime)
        .ThenBy(f => f.VersionTime)
        .Select(f => new
        {
            f.Id,
            f.ValidTime,
            f.VersionTime,
            f.DatasetId,
            f.Region,
            f.SolarMW,
            f.WindMW,
            f.IsHistoricalVersion,
            f.RawDataId
        })
        .ToListAsync();

    var grouped = rows
        .GroupBy(r => r.ValidTime)
        .Select(g => new
        {
            ValidTimeUtc = g.Key,
            Versions = g
                .OrderBy(v => v.VersionTime)
                .Select(v => new
                {
                    v.Id,
                    VersionTimeUtc = v.VersionTime,
                    v.DatasetId,
                    v.Region,
                    v.SolarMW,
                    v.WindMW,
                    v.IsHistoricalVersion,
                    v.RawDataId
                })
                .ToList()
        })
        .OrderBy(x => x.ValidTimeUtc)
        .ToList();

    return Results.Ok(grouped);
});

// GET /api/forecasts/regions?includeHistoricalData=false
app.MapGet("/api/forecasts/regions", async (
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    var include = includeHistoricalData ?? false; // default = false

    var query = db.Forecasts.AsNoTracking();

    if (!include)
    {
        query = query.Where(f => !f.IsHistoricalVersion);
    }

    var regions = await query
        .Select(f => f.Region)
        .Distinct()
        .OrderBy(r => r)
        .ToListAsync();

    return Results.Ok(regions);
});

// latest forecast for a single region (combined solar+wind)
// GET /api/forecasts/latest?region=Flanders&includeHistoricalData=false
app.MapGet("/api/forecasts/latest", async (
    string region,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var include = includeHistoricalData ?? false;

    var series = await ForecastQueryHelper.QueryCombinedForecastAsync(
        db,
        region,
        from: null,
        to: null,
        includeHistoricalData: include);

    if (series.Count == 0)
    {
        return Results.NotFound($"No forecasts found for region '{region}'.");
    }

    // series is ordered ascending by ValidTimeUtc
    var latest = series[^1];
    return Results.Ok(latest);
});

// latest forecast for ALL regions (combined solar+wind)
// GET /api/forecasts/latest/all?includeHistoricalData=false
app.MapGet("/api/forecasts/latest/all", async (
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    var include = includeHistoricalData ?? false;

    var baseQuery = db.Forecasts.AsNoTracking();
    if (!include)
    {
        baseQuery = baseQuery.Where(f => !f.IsHistoricalVersion);
    }

    var regions = await baseQuery
        .Select(f => f.Region)
        .Distinct()
        .OrderBy(r => r)
        .ToListAsync();

    var results = new List<object>();

    foreach (var region in regions)
    {
        var series = await ForecastQueryHelper.QueryCombinedForecastAsync(
            db,
            region,
            from: null,
            to: null,
            includeHistoricalData: include);

        if (series.Count == 0)
            continue;

        var latest = series[^1];

        results.Add(new
        {
            Region = region,
            Forecast = latest
        });
    }

    return Results.Ok(results);
});

// raw forecast rows 
// GET /api/forecasts/raw?region=Flanders&from=...&to=...&includeHistoricalData=false
app.MapGet("/api/forecasts/raw", async (
    string region,
    DateTime? from,
    DateTime? to,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var include = includeHistoricalData ?? false;

    var query = db.Forecasts
        .AsNoTracking()
        .Where(f => f.Region == region);

    if (!include)
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
        .OrderBy(f => f.ValidTime)
        .Select(f => new
        {
            f.Id,
            f.Region,
            f.ValidTime,
            f.SolarMW,
            f.WindMW,
            f.IsHistoricalVersion
        })
        .ToListAsync();

    return Results.Ok(rows);
});

// version history for a specific (Region, ValidTime)
// GET /api/forecasts/versions?region=Flanders&validTime=2025-12-07T06:30:00Z
app.MapGet("/api/forecasts/versions", async (
    string region,
    DateTime validTime,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var validTimeUtc = DateTime.SpecifyKind(validTime, DateTimeKind.Utc);

    var rows = await db.Forecasts
        .AsNoTracking()
        .Where(f => f.Region == region && f.ValidTime == validTimeUtc)
        .OrderBy(f => f.IsHistoricalVersion) // historical first, latest last
        .ThenBy(f => f.Id)
        .Select(f => new
        {
            f.Id,
            f.Region,
            f.ValidTime,
            f.SolarMW,
            f.WindMW,
            f.IsHistoricalVersion
        })
        .ToListAsync();

    if (rows.Count == 0)
    {
        return Results.NotFound($"No versions found for region '{region}' at {validTimeUtc:O}.");
    }

    return Results.Ok(rows);
});

// version summary over a range
// GET /api/forecasts/versions/summary?region=Flanders&from=...&to=...
app.MapGet("/api/forecasts/versions/summary", async (
    string? region,
    DateTime? from,
    DateTime? to,
    AppDbContext db) =>
{
    var query = db.Forecasts.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(region))
    {
        query = query.Where(f => f.Region == region);
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

    var summary = await query
        .GroupBy(f => new { f.Region, f.ValidTime })
        .Select(g => new
        {
            g.Key.Region,
            g.Key.ValidTime,
            VersionCount = g.Count(),
            HistoricalCount = g.Count(f => f.IsHistoricalVersion),
            HasHistorical = g.Any(f => f.IsHistoricalVersion)
        })
        .OrderBy(x => x.Region)
        .ThenBy(x => x.ValidTime)
        .ToListAsync();

    return Results.Ok(summary);
});

// regional summary over a range
// GET /api/forecasts/summary?region=Flanders&from=...&to=...&includeHistoricalData=false
app.MapGet("/api/forecasts/summary", async (
    string region,
    DateTime? from,
    DateTime? to,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var include = includeHistoricalData ?? false;

    var combined = await ForecastQueryHelper.QueryCombinedForecastAsync(
        db,
        region,
        from,
        to,
        include);

    if (combined.Count == 0)
    {
        return Results.NotFound($"No forecasts found for region '{region}' in the specified range.");
    }

    var solarValues = combined
        .Where(x => x.SolarMW.HasValue)
        .Select(x => x.SolarMW!.Value)
        .ToList();

    var windValues = combined
        .Where(x => x.WindMW.HasValue)
        .Select(x => x.WindMW!.Value)
        .ToList();

    var totalValues = combined
        .Select(x => x.TotalMW)
        .ToList();

    double? solarMin = solarValues.Count > 0 ? solarValues.Min() : null;
    double? solarMax = solarValues.Count > 0 ? solarValues.Max() : null;
    double? solarAvg = solarValues.Count > 0 ? solarValues.Average() : null;

    double? windMin = windValues.Count > 0 ? windValues.Min() : null;
    double? windMax = windValues.Count > 0 ? windValues.Max() : null;
    double? windAvg = windValues.Count > 0 ? windValues.Average() : null;

    double totalMin = totalValues.Min();
    double totalMax = totalValues.Max();
    double totalAvg = totalValues.Average();

    var dto = new RegionalForecastSummaryDto(
        Region: region,
        FromUtc: combined.First().ValidTimeUtc,
        ToUtc: combined.Last().ValidTimeUtc,
        PointCount: combined.Count,
        SolarMinMW: solarMin,
        SolarMaxMW: solarMax,
        SolarAvgMW: solarAvg,
        WindMinMW: windMin,
        WindMaxMW: windMax,
        WindAvgMW: windAvg,
        TotalMinMW: totalMin,
        TotalMaxMW: totalMax,
        TotalAvgMW: totalAvg
    );

    return Results.Ok(dto);
});

// region comparison (multiple regions, each with its own time series)
// GET /api/forecasts/compare-regions?regions=Flanders,Wallonia&from=...&to=...&includeHistoricalData=false
app.MapGet("/api/forecasts/compare-regions", async (
    string regions,
    DateTime? from,
    DateTime? to,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(regions))
    {
        return Results.BadRequest("At least one region is required.");
    }

    var regionList = regions
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    if (regionList.Count == 0)
    {
        return Results.BadRequest("At least one valid region is required.");
    }

    var include = includeHistoricalData ?? false;

    var results = new List<object>();

    foreach (var region in regionList)
    {
        var series = await ForecastQueryHelper.QueryCombinedForecastAsync(
            db,
            region,
            from,
            to,
            include);

        results.Add(new
        {
            Region = region,
            Series = series
        });
    }

    return Results.Ok(results);
});

// peak load times (top N timestamps with highest TotalMW)
// GET /api/forecasts/peaks?region=Flanders&from=...&to=...&top=10&includeHistoricalData=false
app.MapGet("/api/forecasts/peaks", async (
    string region,
    DateTime? from,
    DateTime? to,
    int top,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    if (top <= 0)
    {
        top = 10;
    }

    var include = includeHistoricalData ?? false;

    var combined = await ForecastQueryHelper.QueryCombinedForecastAsync(
        db,
        region,
        from,
        to,
        include);

    if (combined.Count == 0)
    {
        return Results.NotFound($"No forecasts found for region '{region}' in the specified range.");
    }

    var peaks = combined
        .OrderByDescending(x => x.TotalMW)
        .ThenBy(x => x.ValidTimeUtc)
        .Take(Math.Min(top, combined.Count))
        .ToList();

    return Results.Ok(peaks);
});

app.MapGet("/api/forecasts/export", async (
    string region,
    DateTime? from,
    DateTime? to,
    string? format,
    bool? includeHistoricalData,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var include = includeHistoricalData ?? false;
    var fmt = string.IsNullOrWhiteSpace(format)
        ? "csv"
        : format.Trim().ToLowerInvariant();

    var combined = await ForecastQueryHelper.QueryCombinedForecastAsync(
        db,
        region,
        from,
        to,
        include);

    if (combined.Count == 0)
    {
        return Results.NotFound($"No forecasts found for region '{region}' in the specified range.");
    }

    if (fmt == "json")
    {
        return Results.Ok(combined);
    }

    if (fmt != "csv")
    {
        return Results.BadRequest("Unsupported format. Use 'csv' or 'json'.");
    }

    var sb = new StringBuilder();
    sb.AppendLine("ValidTimeUtc,SolarMW,WindMW,TotalMW");

    foreach (var row in combined)
    {
        var time = row.ValidTimeUtc.ToString("o", CultureInfo.InvariantCulture);
        var solar = row.SolarMW?.ToString(CultureInfo.InvariantCulture) ?? "";
        var wind = row.WindMW?.ToString(CultureInfo.InvariantCulture) ?? "";
        var total = row.TotalMW.ToString(CultureInfo.InvariantCulture);

        sb.Append(time).Append(',')
          .Append(solar).Append(',')
          .Append(wind).Append(',')
          .Append(total).AppendLine();
    }

    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
    var fileName = $"forecasts_{region}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

    return Results.File(bytes, "text/csv", fileName);
});
// measured series only
// GET /api/measured/combined?region=Flanders&from=2025-12-01&to=2025-12-05
app.MapGet("/api/measured/combined", async (
    string region,
    DateTime? from,
    DateTime? to,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var series = await TimeSeriesQueryHelper.QueryCombinedHistoricalPointAsync(db, region, from, to);
    return Results.Ok(series);
});

// overlay: historical if available, otherwise forecast
// GET /api/timeseries/overlay?region=Flanders&from=2025-12-01&to=2025-12-05
app.MapGet("/api/timeseries/overlay", async (
    string region,
    DateTime? from,
    DateTime? to,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest("Region is required.");
    }

    var series = await TimeSeriesQueryHelper.QueryOverlayCombinedAsync(db, region, from, to);
    return Results.Ok(series);
});

// -------------------- SPA FALLBACK --------------------
// Any request that did not match an /api/... route or a static file
// will fall back to serving wwwroot/index.html.
// This is what makes client-side routing (e.g., /dashboard, /graphs) work.
app.MapFallbackToFile("index.html");

app.Run();

