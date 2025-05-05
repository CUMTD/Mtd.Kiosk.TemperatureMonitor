using System.ComponentModel.DataAnnotations;

namespace Mtd.Kiosk.TempMonitor.Vertiv.Config;

public class VertivConfig
{
    public const string ConfigSectionName = "VertivSensorWorker";
    public bool Enabled { get; set; }

    [Required]
    public required string SensorIp { get; set; }

    [Required]
    public required int SensorType { get; set; }
}

