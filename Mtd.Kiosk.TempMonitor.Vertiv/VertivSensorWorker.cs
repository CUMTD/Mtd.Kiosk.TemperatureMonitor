using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mtd.Kiosk.TempMonitor.Core;
using Mtd.Kiosk.TempMonitor.Core.Config;
using Mtd.Kiosk.TempMonitor.Vertiv.Config;
using System.Text.Json;

namespace Mtd.Kiosk.TempMonitor.Vertiv
{
    public sealed class VertivSensorWorker : TemperatureMonitorBackgroundService
    {
        public const string KEY = "VERTIV";
        private readonly VertivConfig _sensorConfig;

        public VertivSensorWorker(
            IOptions<VertivConfig> sensorConfig,
            HttpClient httpClient,
            IOptions<TemperatureMonitorConfiguration> tempMonitorConfig,
            ILogger<VertivSensorWorker> logger
            ) : base("VertivMonitor", httpClient, tempMonitorConfig, logger)
        {
            ArgumentNullException.ThrowIfNull(sensorConfig, nameof(sensorConfig));
            ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));
            ArgumentNullException.ThrowIfNull(tempMonitorConfig, nameof(tempMonitorConfig));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _sensorConfig = sensorConfig.Value;
        }

        protected override async Task<(int temperature, int humidity)> ReadDataFromSensor(CancellationToken cancellationToken)
        {
            int? temperature = null;
            int? humidity = null;

            var response = await _httpClient.GetAsync($"http://{_sensorConfig.SensorIp}/api/dev", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var deserializedResponse = JsonSerializer.Deserialize<SensorResponse>(json);

            if (deserializedResponse == null)
            {
                throw new Exception("No data in Vertiv sensor response.");
            }

            var device = deserializedResponse.Data.First();
            var entity = device.Value.Entities.First();


            foreach (var measurement in entity.Value.Measurements.Values)
            {
                if (measurement.Type == "temperature")
                {
                    temperature = (int)measurement.Value;
                }
                else if (measurement.Type == "humidity")
                {
                    humidity = (int)measurement.Value;

                }
            }
            _logger.LogInformation("Data fetched successfully");

            if (temperature.HasValue && humidity.HasValue)
            {
                return (temperature.Value, humidity.Value);
            }

            throw new Exception("Temperature or humidity did not have value.");
        }
    }
}

