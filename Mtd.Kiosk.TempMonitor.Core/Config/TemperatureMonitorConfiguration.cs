using System.ComponentModel.DataAnnotations;

namespace Mtd.Kiosk.TempMonitor.Core.Config;


public class TemperatureMonitorConfiguration
{
    public const string SectionName = "TemperatureMonitor";
    public string? DataCollectionEndpoint { get; set; }
    public int ReportingIntervalSeconds { get; set; }

    [Required]
    public required string KioskId { get; set; }
    [Required]
    public string? ApiKey { get; set; }

}

