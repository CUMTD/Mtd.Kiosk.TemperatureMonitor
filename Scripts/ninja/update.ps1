<# 
.SYNOPSIS
    Updates (or installs) the Temperature Monitor Service on a single kiosk to the latest release.

.DESCRIPTION
    1. Stops and removes any existing "MTD Temperature Monitor" service.
    2. Downloads & extracts the latest service-vX.X.X.zip from GitHub.
    3. Copies `publish_output` to C:\Services\TemperatureMonitor-vX.X.X.
    4. Registers the new service (StartupType = Manual).
    5. Applies Ninja environment variables (KioskId, VertivIp, AdafruitEnabled).
    6. Sets service to StartupType = Automatic.
    7. Starts the service.

.NOTES
    Ninja RMM must provide:
      - $env:kioskId
      - $env:vertivIp   (optional)
      - $env:adafruitEnabled
    Run as Administrator.
#>

# ─────── [ Configuration ] ───────

$owner = 'CUMTD'
$repo = 'Mtd.Kiosk.TemperatureMonitor'
$serviceName = 'MTD Temperature Monitor'
$serviceExe = 'Mtd.Kiosk.TempMonitor.Service.exe'
$installRoot = "$env:SystemDrive\Services"
$envPrefix = 'Kiosk_Temp_Sensor__'

function Throw-Terminate {
    param($msg)
    Write-Error $msg
    exit 1
}

# ─────── [ 1. STOP & REMOVE EXISTING SERVICE ] ───────

Write-Host "Checking for existing service '$serviceName'..."
$existingSvc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($existingSvc) {
    if ($existingSvc.Status -ne 'Stopped') {
        Write-Host "  • Stopping service..."
        Stop-Service -Name $serviceName -Force -ErrorAction Stop
        Start-Sleep -Seconds 2
    }

    Write-Host "  • Removing existing service..."
    if (Get-Command Remove-Service -ErrorAction SilentlyContinue) {
        Remove-Service -Name $serviceName -Force
    }
    else {
        $svcWmi = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
        if ($svcWmi) {
            $svcWmi.Delete() | Out-Null
        }
    }

    Start-Sleep -Seconds 2
}
else {
    Write-Host "  • No existing service found; proceeding."
}

# ─────── [ 2. FETCH LATEST GITHUB RELEASE ] ───────

$apiUrl = "https://api.github.com/repos/$owner/$repo/releases/latest"
Write-Host "Querying GitHub for latest release of $owner/$repo..."
try {
    $releaseInfo = Invoke-RestMethod -Uri $apiUrl `
        -Headers @{ 'User-Agent' = 'PowerShellScript' } `
        -ErrorAction Stop
}
catch {
    Throw-Terminate "Failed to query GitHub API: $_"
}

$asset = $releaseInfo.assets | Where-Object { $_.name -match '^service-v[\d\.]+\.zip$' }
if (-not $asset) {
    Throw-Terminate "No asset named 'service-vX.X.X.zip' found in the latest release."
}

$assetName = $asset.name
$downloadUrl = $asset.browser_download_url
Write-Host "Found asset: $assetName"

if ($assetName -match '^service-v([\d\.]+)\.zip$') {
    $version = $Matches[1]
}
else {
    Throw-Terminate "Unexpected asset name format: $assetName"
}

# ─────── [ 3. DOWNLOAD & EXTRACT ] ───────

$tempDir = Join-Path $env:TEMP "TempMonitorDeploy"
$tempZip = Join-Path $tempDir "service-v$version.zip"
$extractDir = Join-Path $tempDir "extracted"
$installDir = Join-Path $installRoot "TemperatureMonitor-v$version"

if (Test-Path $tempDir) {
    Write-Host "Cleaning up old temp directory: $tempDir"
    Remove-Item -Recurse -Force $tempDir
}
New-Item -ItemType Directory -Path $extractDir | Out-Null

Write-Host "Downloading ZIP to $tempZip..."
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing -ErrorAction Stop
}
catch {
    Throw-Terminate "Failed to download ZIP from GitHub: $_"
}

Write-Host "Extracting ZIP to $extractDir..."
try {
    Expand-Archive -LiteralPath $tempZip -DestinationPath $extractDir -Force
}
catch {
    Throw-Terminate "Failed to extract ZIP: $_"
}

# ─────── [ 4. COPY publish_output → INSTALL DIR ] ───────

$publishFolder = Get-ChildItem -Path $extractDir -Recurse -Directory `
| Where-Object { $_.Name -eq 'publish_output' } `
| Select-Object -First 1

if (-not $publishFolder) {
    Throw-Terminate "Could not find a 'publish_output' folder in the extracted ZIP."
}

Write-Host "Found publish_output at: $($publishFolder.FullName)"
if (Test-Path $installDir) {
    Write-Host "Removing old install directory: $installDir"
    Remove-Item -Recurse -Force $installDir
}
Write-Host "Creating install directory: $installDir"
New-Item -ItemType Directory -Path $installDir | Out-Null

Write-Host "Copying files from publish_output → $installDir"
Copy-Item -Path (Join-Path $publishFolder.FullName '*') -Destination $installDir -Recurse -Force

# ─────── [ 5. REGISTER NEW SERVICE ] ───────

$exePath = Join-Path $installDir $serviceExe
if (-not (Test-Path $exePath)) {
    Throw-Terminate "Could not find '$serviceExe' in '$installDir'. Ensure publish produced it."
}

Write-Host "Registering service '$serviceName' (StartupType = Automatic) → $exePath"
New-Service `
    -Name           $serviceName `
    -BinaryPathName "`"$exePath`"`" `
    -DisplayName    $serviceName `
    -StartupType    Automatic

Write-Host "Starting service '$serviceName'..."
Start-Service -Name $serviceName -ErrorAction Stop

Write-Host "`nUpdate script completed successfully. Service is running version $version."
