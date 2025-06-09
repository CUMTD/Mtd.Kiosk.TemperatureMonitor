using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mtd.Kiosk.TempMonitor.Core.Config;

namespace Mtd.Kiosk.TempMonitor.Core
{
    public abstract class TemperatureMonitorBackgroundService : BackgroundService, IHostedService
    {
        protected readonly string _monitorName;
        protected readonly HttpClient _httpClient;
        protected readonly TemperatureMonitorConfiguration _config;
        protected readonly ILogger<TemperatureMonitorBackgroundService> _logger;

        protected TemperatureMonitorBackgroundService(string monitorName, HttpClient httpClient, IOptions<TemperatureMonitorConfiguration> config, ILogger<TemperatureMonitorBackgroundService> logger)
        {
            ArgumentNullException.ThrowIfNull(monitorName, nameof(monitorName));
            ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));
            ArgumentNullException.ThrowIfNull(config, nameof(config));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _monitorName = monitorName;
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
        }

        protected virtual Task SetupSensor(CancellationToken cancellationToken) => Task.CompletedTask;

        protected virtual Task TearDownSensor() => Task.CompletedTask;

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{monitorName} started.", _monitorName);

            try
            {
                _logger.LogDebug("Setting Up Sensor");
                await SetupSensor(cancellationToken);
                _logger.LogDebug("Sensor Ready");

            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unable to set up sensor.");
                throw;
            }

            try
            {
                await RunMonitorLoop(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Monitor loop threw an unexpected exception. Exiting.");
            }
            finally
            {
                _logger.LogDebug("Tearing down sensor");
                await TearDownSensor();
                _logger.LogDebug("Done tearing down sensor.");
            }


            _logger.LogInformation("{monitorName} stopping.", _monitorName);

        }

        private async Task RunMonitorLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.ReportingIntervalSeconds), cancellationToken);

                int temperature;
                int humidity;
                try
                {
                    (temperature, humidity) = await ReadDataFromSensor(cancellationToken);
                    _logger.LogTrace($"Sensor reports temp. {temperature}, humidity {humidity}%");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading data from {monitorName}", _monitorName);
                    continue;
                }

                try
                {
                    await SendTemperatureToApi(temperature, humidity, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending data from {monitorName}", _monitorName);
                }
            }
        }

        protected abstract Task<(int temperature, int humidity)> ReadDataFromSensor(CancellationToken cancellationToken);

        private async Task SendTemperatureToApi(int temperature, int humidity, CancellationToken cancellationToken)
        {
            var uri = new Uri($"{_config.DataCollectionEndpoint}/{_config.KioskId}?temp={temperature}&humidity={humidity}");
            _logger.LogTrace("POSTing to: {uri}", uri.ToString());
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Headers.Add("X-ApiKey", _config.ApiKey);  // Ensure ApiKey is in config
                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                _logger.LogDebug("Data sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending data");
            }
        }

        protected static int CelsiusToFahrenheit(double celsius) => (int)Math.Round(celsius * 9 / 5 + 32);
    }
}
