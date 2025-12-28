using System;
using System.ComponentModel.DataAnnotations;

namespace Elia.Shared.Models;
public class HistoricalPoint
{
    public int Id { get; set; }

    // e.g. ods032 (PV historical), ods031 (wind historical)
    [Required]
    public string DatasetId { get; set; } = null!;

    // "solar" or "wind" 
    [Required]
    public string EnergyType { get; set; } = null!;

    [Required]
    public string Region { get; set; } = null!;

    // UTC
    public DateTime ValidTime { get; set; }

    public double MeasuredMW { get; set; }

    // wind historical has extra dimensions, keep nullable for solar.
    public string? OffshoreOnshore { get; set; }       // "Offshore" / "Onshore"
    public string? GridConnectionType { get; set; }     // "Elia" / "Dso" etc.

    public int RawDataId { get; set; }
    public RawData RawData { get; set; } = null!;

}
