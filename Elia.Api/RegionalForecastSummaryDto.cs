namespace Elia.Api.Dtos;

public record RegionalForecastSummaryDto(
    string Region,
    DateTime FromUtc,
    DateTime ToUtc,
    int PointCount,
    double? SolarMinMW,
    double? SolarMaxMW,
    double? SolarAvgMW,
    double? WindMinMW,
    double? WindMaxMW,
    double? WindAvgMW,
    double TotalMinMW,
    double TotalMaxMW,
    double TotalAvgMW
);

