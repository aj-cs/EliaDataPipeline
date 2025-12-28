namespace Elia.Api.Dtos;

public record CombinedForecastDto(
        DateTime ValidTimeUtc,
        double? SolarMW,
        double? WindMW,
        double TotalMW
);
