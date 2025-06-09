<#
.SYNOPSIS
    Configures and starts the Temperature Monitor Service using Ninja RMM variables.

.DESCRIPTION
    1. Stops "MTD Temperature Monitor" if running.
    2. Sets environment variables (Machine scope) for:
       - TemperatureMonitor:KioskId
       - VertivSensorWorker:SensorIp  (only if provided)
       - VertivSensorWorker:Enabled
       - AdafruitSensorWorker:Enabled
    3. Sets the service’s StartupType to Automatic.
    4. Starts the service.

.NOTES
    Ninja RMM must provide:
      - $env:kioskId
      - $env:vertivIp   (optional)
      - $env:adafruitEnabled
      - $env:apiKey
    Run as Administrator.
#>

$serviceName = 'MTD Temperature Monitor'
$envPrefix = 'Kiosk_Temp_Sensor__'

function Throw-Terminate {
    param($msg)
    Write-Error $msg
    exit 1
}

# ─────── [ 1. STOP SERVICE IF RUNNING ] ───────

Write-Host "Checking for service '$serviceName'..."
$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($svc) {
    if ($svc.Status -ne 'Stopped') {
        Write-Host "  • Service is running. Stopping..."
        Stop-Service -Name $serviceName -Force -ErrorAction Stop
        Start-Sleep -Seconds 2
    }
    else {
        Write-Host "  • Service already stopped."
    }
}
else {
    Write-Error "Service '$serviceName' not found. Cannot configure."
}

# ─────── [ 2. APPLY NINJA ENVIRONMENT VARIABLES ] ───────

Write-Host "Configuring environment variables..."

# 2a) TemperatureMonitor:KioskId
if ($env:kioskId) {
    $key = $envPrefix + 'TemperatureMonitor__KioskId'
    $val = $env:kioskId
    Write-Host "  • [$key] = '$val'"
    [Environment]::SetEnvironmentVariable($key, $val, 'Machine')
}
else {
    Throw-Terminate "Missing required Ninja variable: `\$env:kioskId."
}

# 2b) TemperatureMonitor:apiKey
if ($env:kioskId) {
    $key = $envPrefix + 'TemperatureMonitor__ApiKey'
    $val = $env:apiKey
    Write-Host "  • [$key] = '$val'"
    [Environment]::SetEnvironmentVariable($key, $val, 'Machine')
}
else {
    Throw-Terminate "Missing required Ninja variable: `\$env:kioskId."
}

# 2c) VertivSensorWorker
if ($env:vertivIp) {
    $keyIp = "$envPrefix`VertivSensorWorker__SensorIp"
    $keyEnabled = "$envPrefix`VertivSensorWorker__Enabled"
    Write-Host "  • Setting [$keyIp] = '$env:vertivIp'"
    Write-Host "  • Setting [$keyEnabled] = 'true'"
    [Environment]::SetEnvironmentVariable($keyIp, $env:vertivIp, 'Machine')
    [Environment]::SetEnvironmentVariable($keyEnabled, 'true', 'Machine')
}
else {
    $keyEnabled = "$envPrefix`VertivSensorWorker__Enabled"
    Write-Host "  • vertivIp not provided; setting [$keyEnabled] = 'false'"
    [Environment]::SetEnvironmentVariable($keyEnabled, 'false', 'Machine')
}

# 2d) AdafruitSensorWorker:Enabled (required)
if ($env:adafruitEnabled) {
    $keyAdafruit = "$envPrefix`AdafruitSensorWorker__Enabled"
    Write-Host "  • Setting [$keyAdafruit] = '$env:adafruitEnabled'"
    [Environment]::SetEnvironmentVariable($keyAdafruit, $env:adafruitEnabled, 'Machine')
}
else {
    Throw-Terminate "Missing required Ninja variable: `\$env:adafruitEnabled."
}

# ─────── [ 3. SET SERVICE TO AUTO AND START ] ───────

if ($svc) {
    Write-Host "Setting StartupType to Automatic for '$serviceName'..."
    Set-Service -Name $serviceName -StartupType Automatic

    Write-Host "Starting service '$serviceName'..."
    Start-Service -Name $serviceName -ErrorAction Stop

    Write-Host "`nConfiguration and start script completed successfully."
}
else {
    Throw-Terminate "Service '$serviceName' not found. Cannot set StartupType or start."
}


# ─────── [ 4. REBOOT ] ───────

Restart-Computer -Force -ErrorAction SilentlyContinue