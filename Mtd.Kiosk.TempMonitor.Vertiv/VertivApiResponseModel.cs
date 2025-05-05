using System.Text.Json.Serialization;

public class SensorResponse
{
    [JsonPropertyName("data")]
    public Dictionary<string, DeviceData> Data { get; set; }

    [JsonPropertyName("retCode")]
    public int RetCode { get; set; }

    [JsonPropertyName("retMsg")]
    public string RetMsg { get; set; }
}

public class DeviceData
{
    [JsonPropertyName("entity")]
    public Dictionary<string, Entity> Entities { get; set; }

    // Remaining properties are included but not required for core functionality
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("alarm")]
    public Alarm Alarm { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Entity
{
    [JsonPropertyName("measurement")]
    public Dictionary<string, Measurement> Measurements { get; set; }
}

public class Measurement
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("value")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double Value { get; set; }

    [JsonPropertyName("units")]
    public string Units { get; set; }
}

public class Alarm
{
    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; }
}