using System.ComponentModel.DataAnnotations;

public class AdafruitConfig
{
    public const string ConfigSectionName = "AdafruitSensorWorker";
    public bool Enabled { get; set; }
    [Required]
    public required int SensorType { get; set; }
    [Required]
    public required string AdafruitVendorId { get; set; }
    [Required]
    public required string AdafruitProductId { get; set; }
}
