
namespace Elia.Shared.Models;

public class Forecast
{
    public int Id { get; set; }

    public string DatasetId { get; set; } = null!; // ods087 == pv

    public string Region { get; set; } = null!;
    // for when the forecast applies
    public DateTime ValidTime { get; set; }
    // for when elia published this forecast
    public DateTime VersionTime { get; set; }

    public double? SolarMW { get; set; }
    public double? WindMW { get; set; }

    public string Horizon { get; set; } = "mostrecent";

    public bool IsHistoricalVersion { get; set; } = false;


    // foreign key pointing back to RawData row
    //
    public int RawDataId { get; set; }
    public RawData RawData { get; set; } = null!;
}
