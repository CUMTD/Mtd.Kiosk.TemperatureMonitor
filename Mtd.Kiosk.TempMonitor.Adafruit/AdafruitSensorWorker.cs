using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mtd.Kiosk.TempMonitor.Core;
using Mtd.Kiosk.TempMonitor.Core.Config;
using System.IO.Ports;
using System.Management;
using System.Runtime.Versioning;

namespace Mtd.Kiosk.TempMonitor.Adafruit
{
    public sealed class AdafruitSensorWorker : TemperatureMonitorBackgroundService
    {
        public const string KEY = "ADAFRUIT";
        private const string QueryString = "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'";
        private readonly AdafruitConfig _sensorConfig;
        private SerialPort? _serialPort;

        public AdafruitSensorWorker(
            IOptions<AdafruitConfig> sensorConfig,
            HttpClient httpClient,
            IOptions<TemperatureMonitorConfiguration> tempMonitorConfig,
            ILogger<AdafruitSensorWorker> logger
        ) : base("Adafruit SHT45", httpClient, tempMonitorConfig, logger)
        {
            ArgumentNullException.ThrowIfNull(sensorConfig, nameof(sensorConfig));
            ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));
            ArgumentNullException.ThrowIfNull(tempMonitorConfig, nameof(tempMonitorConfig));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _sensorConfig = sensorConfig.Value;
        }

        [SupportedOSPlatform("windows")]
        protected override async Task SetupSensor(CancellationToken cancellationToken)
        {
            var portName = FindComPort();

            if (portName == null)
                return;

            InitializeSerialPort(portName);
        }


        protected override Task TearDownSensor()
        {
            _serialPort?.Close();
            return Task.CompletedTask;
        }

        protected override async Task<(int temperature, int humidity)> ReadDataFromSensor(CancellationToken cancellationToken)
        {
            const byte SEPARATOR = 255;

            try
            {
                if (_serialPort == null)
                {
                    throw new InvalidOperationException("Serial port is null");
                }

                var bytesToRead = _serialPort.BytesToRead;

                var buffer = new int[bytesToRead];
                for (var i = 0; i < bytesToRead; i++)
                {
                    buffer[i] = _serialPort.ReadByte();
                }

                var separated = buffer
                    .Select((b, i) => new { Byte = b, Index = i })
                    .Where(x => x.Byte == SEPARATOR && x.Index + 2 < buffer.Length)
                    .Select(x => (CelsiusToFahrenheit(buffer[x.Index + 1]), buffer[x.Index + 2]))
                    .ToArray();

                if (separated.Length == 0)
                {
                    throw new Exception("Buffer does not yet have a complete set of data.");
                }

                // we have read the current data.
                // so we discard it
                _serialPort.DiscardInBuffer();

                return separated.Last();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Serial port error");
                await HandlePortDisconnection(cancellationToken);
                throw;
            }
        }

        [SupportedOSPlatform("windows")]
        private string? FindComPort()
        {
            var searcher = new ManagementObjectSearcher(QueryString);
            string? portName = null;
            foreach (ManagementObject obj in searcher.Get())
            {
                string name = (string)obj["Caption"];
                string pnpId = (string)obj["PNPDeviceID"];
                if (pnpId != null && pnpId.Contains(_sensorConfig.AdafruitProductId) && pnpId.Contains(_sensorConfig.AdafruitVendorId))
                {
                    // extract the COM port from the name string
                    var comPort = name.Substring(name.IndexOf("COM")).TrimEnd(')');
                    portName = comPort;
                    _logger.LogInformation($"Adafruit sensor found at {comPort}");
                    break;

                }
            }

            return portName;
        }

        private void InitializeSerialPort(string portName)
        {
            _serialPort = new SerialPort(portName, 115200)
            {
                DtrEnable = true,
                RtsEnable = false,
                ReadTimeout = 10000,
            };
            _serialPort.Open();
            _logger.LogInformation("Connected to {Port}", portName);
        }

        private async Task HandlePortDisconnection(CancellationToken token)
        {
            // todo: log this
            _serialPort?.Close();
            _logger.LogWarning("Attempting to reconnect...");
            var newPort = FindComPort();
            if (newPort != null)
            {
                InitializeSerialPort(newPort);
            }
        }
    }
}
