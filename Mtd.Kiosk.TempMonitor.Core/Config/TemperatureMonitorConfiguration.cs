using System.ComponentModel.DataAnnotations;

namespace Mtd.Kiosk.TempMonitor.Config;


public class TemperatureMonitorConfiguration
{
    public const string SectionName = "TemperatureMonitor";
    public string? DataCollectionEndpoint { get; set; }
    public int ReportingIntervalSeconds { get; set; }

    [Required]
    public string KioskId { get; set; } = "";
    public string? ApiKey { get; set; }

}

