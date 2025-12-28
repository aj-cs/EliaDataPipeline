namespace Elia.Api.Dtos;

public record ForecastVersionComparisonDto(
    string Region,
    DateTime ValidTimeUtc,
    int Version1Id,
    int Version2Id,
    double? SolarMW1,
    double? SolarMW2,
    double SolarDiff,
    double? WindMW1,
    double? WindMW2,
    double WindDiff,
    double TotalMW1,
    double TotalMW2,
    double TotalDiff,
    bool IsHistoricalVersion1,
    bool IsHistoricalVersion2
);
