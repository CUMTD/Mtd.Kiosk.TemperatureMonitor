using Mtd.Kiosk.TempMonitor.Adafruit;
using Mtd.Kiosk.TempMonitor.Config;
using Mtd.Kiosk.TempMonitor.Core;
using Mtd.Kiosk.TempMonitor.Vertiv;
using Mtd.Kiosk.TempMonitor.Vertiv.Config;

var builder = Host.CreateApplicationBuilder(args);

// Windows service settings
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MTD Temperature Monitor";
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<TemperatureMonitorBackgroundService>();
    builder.Configuration.AddUserSecrets<TemperatureMonitorConfiguration>();
}
builder.Configuration.AddEnvironmentVariables("Kiosk_Temp_Sensor");

// HTTP clients
builder.Services.AddHttpClient<TemperatureMonitorBackgroundService>(client =>
{
    client.DefaultRequestHeaders.Add("X-ApiKey", builder.Configuration["TemperatureMonitor:ApiKey"]);
});

// Bind options and validate on start
builder.Services.Configure<AdafruitConfig>(builder.Configuration.GetSection(AdafruitConfig.ConfigSectionName));
builder.Services.Configure<VertivConfig>(builder.Configuration.GetSection(VertivConfig.ConfigSectionName));
builder.Services.Configure<TemperatureMonitorConfiguration>(builder.Configuration.GetSection(TemperatureMonitorConfiguration.SectionName));

builder.Services.AddOptionsWithValidateOnStart<AdafruitConfig>(AdafruitConfig.ConfigSectionName);
builder.Services.AddOptionsWithValidateOnStart<VertivConfig>(VertivConfig.ConfigSectionName);
builder.Services.AddOptionsWithValidateOnStart<TemperatureMonitorConfiguration>(TemperatureMonitorConfiguration.SectionName);

// Register keyed services
builder.Services.AddKeyedSingleton<TemperatureMonitorBackgroundService, AdafruitSensorWorker>(AdafruitSensorWorker.KEY);
builder.Services.AddKeyedSingleton<TemperatureMonitorBackgroundService, VertivSensorWorker>(VertivSensorWorker.KEY);

// Read configuration early
var vertivConfig = builder.Configuration.GetSection(VertivConfig.ConfigSectionName).Get<VertivConfig>();
var adafruitConfig = builder.Configuration.GetSection(AdafruitConfig.ConfigSectionName).Get<AdafruitConfig>();

// Conditionally register hosted services
if (vertivConfig?.Enabled == true)
{
    builder.Services.AddHostedService<VertivSensorWorker>();
}

if (adafruitConfig?.Enabled == true)
{
    builder.Services.AddHostedService<AdafruitSensorWorker>();
}

// Register ITempMonitor collection
builder.Services.AddSingleton<IEnumerable<TemperatureMonitorBackgroundService>>(sp =>
{
    List<TemperatureMonitorBackgroundService> monitors = new();

    if (vertivConfig?.Enabled == true)
        monitors.Add(sp.GetRequiredKeyedService<TemperatureMonitorBackgroundService>(VertivSensorWorker.KEY));

    if (adafruitConfig?.Enabled == true)
        monitors.Add(sp.GetRequiredKeyedService<TemperatureMonitorBackgroundService>(AdafruitSensorWorker.KEY));

    return monitors;
});

var host = builder.Build();
await host.RunAsync();
