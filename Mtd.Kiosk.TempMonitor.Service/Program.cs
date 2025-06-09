using Microsoft.Extensions.Options;
using Mtd.Kiosk.TempMonitor.Adafruit;
using Mtd.Kiosk.TempMonitor.Core;
using Mtd.Kiosk.TempMonitor.Core.Config;
using Mtd.Kiosk.TempMonitor.Vertiv;
using Mtd.Kiosk.TempMonitor.Vertiv.Config;

var builder = Host.CreateApplicationBuilder(args);

// Set up Windows service configuration
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MTD Temperature Monitor";
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Configuration hierarchy
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Configuration
    .AddEnvironmentVariables("Kiosk_Temp_Sensor__");

// Bind and validate options
builder.Services
    .AddOptionsWithValidateOnStart<AdafruitConfig>()
    .BindConfiguration(AdafruitConfig.ConfigSectionName)
    .ValidateDataAnnotations();

builder.Services
    .AddOptionsWithValidateOnStart<VertivConfig>()
    .BindConfiguration(VertivConfig.ConfigSectionName)
    .ValidateDataAnnotations();

builder.Services
    .AddOptionsWithValidateOnStart<TemperatureMonitorConfiguration>()
    .BindConfiguration(TemperatureMonitorConfiguration.SectionName)
    .ValidateDataAnnotations();

// Register HTTP client with API key header
builder.Services.AddHttpClient<TemperatureMonitorBackgroundService>((sp, client) =>
{
    try
    {
        string? key = sp.GetRequiredService<IOptions<TemperatureMonitorConfiguration>>().Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            client.DefaultRequestHeaders.Add("X-ApiKey", key);
        }
    }
    catch (Exception ex)
    {
        var logger = sp.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Failed to configure HTTP client with API key.");
        throw;
    }
});

using (var tempProvider = builder.Services.BuildServiceProvider())
{
    var adafruitConfig = tempProvider.GetRequiredService<IOptions<AdafruitConfig>>().Value;
    var vertivConfig = tempProvider.GetRequiredService<IOptions<VertivConfig>>().Value;

    RegisterSensorWorkers(builder.Services, adafruitConfig, vertivConfig);
}

// Build and run host
var host = builder.Build();
await host.RunAsync();

static void RegisterSensorWorkers(IServiceCollection services, AdafruitConfig adafruit, VertivConfig vertiv)
{
    services.AddKeyedSingleton<TemperatureMonitorBackgroundService, AdafruitSensorWorker>(AdafruitSensorWorker.KEY);
    services.AddKeyedSingleton<TemperatureMonitorBackgroundService, VertivSensorWorker>(VertivSensorWorker.KEY);

    if (adafruit.Enabled)
    {
        services.AddHostedService<AdafruitSensorWorker>();
    }

    if (vertiv.Enabled)
    {
        services.AddHostedService<VertivSensorWorker>();
    }

    services.AddSingleton<IEnumerable<TemperatureMonitorBackgroundService>>(sp =>
    {
        List<TemperatureMonitorBackgroundService> monitors = [];

        if (adafruit.Enabled)
        {
            monitors.Add(sp.GetRequiredKeyedService<TemperatureMonitorBackgroundService>(AdafruitSensorWorker.KEY));
        }

        if (vertiv.Enabled)
        {
            monitors.Add(sp.GetRequiredKeyedService<TemperatureMonitorBackgroundService>(VertivSensorWorker.KEY));
        }

        return monitors;
    });
}
