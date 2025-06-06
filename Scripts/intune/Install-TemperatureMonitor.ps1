<#
.SYNOPSIS
    Installs (or updates) MTD Temperature Monitor to the latest GitHub release, logging every step.

.NOTES
    Must run as Administrator. Intune should call this as the install command:
    powershell.exe -ExecutionPolicy Bypass -NoLogo -NoProfile -File .\Install-TemperatureMonitor.ps1
#>

#region Logging & Initialization
$logFolder = "C:\ProgramData\MTD"
$logPath = Join-Path $logFolder "TempMonitor_install.log"

function Throw-Terminate {
    param([string]$Message, [int]$ExitCode = 1)
    Write-Error "ERROR: $Message"
    Write-Error "    See log: $logPath"
    if ($script:Transcribing) { Stop-Transcript | Out-Null }
    exit $ExitCode
}

if (-not (Test-Path $logFolder)) {
    New-Item -Path $logFolder -ItemType Directory -Force | Out-Null
}

$script:Transcribing = $false
try {
    Start-Transcript -Path $logPath -Force -ErrorAction Stop
    $script:Transcribing = $true
}
catch {
    Write-Warning "Failed to start transcript logging: $_"
}

Write-Host "=== MTD Temperature Monitor Install ==="
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
#endregion

#region Configuration
$owner = 'CUMTD'
$repo = 'Mtd.Kiosk.TemperatureMonitor'
$serviceName = 'MTD Temperature Monitor'
$serviceExe = 'Mtd.Kiosk.TempMonitor.Service.exe'
$installRoot = "$env:SystemDrive\Services"
#endregion

#region Uninstall Existing
Write-Host "--- STEP 1: Uninstall existing version ---"
$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force -ErrorAction Stop
        Start-Sleep -Seconds 2
    }

    if (Get-Command Remove-Service -ErrorAction SilentlyContinue) {
        Remove-Service -Name $serviceName -Force -ErrorAction Stop
    }
    else {
        $svcWmi = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
        if ($svcWmi) { $svcWmi.Delete() | Out-Null }
    }

    Start-Sleep -Seconds 2
}

$oldFolders = Get-ChildItem -Path $installRoot -Directory -Filter 'TemperatureMonitor-v*' -ErrorAction SilentlyContinue
foreach ($folder in $oldFolders) {
    Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction Stop
}
#endregion

#region Fetch Release
Write-Host "--- STEP 2: Fetch GitHub release ---"
$apiUrl = "https://api.github.com/repos/$owner/$repo/releases/latest"
$releaseInfo = Invoke-RestMethod -Uri $apiUrl -Headers @{ 'User-Agent' = 'PowerShellScript' } -ErrorAction Stop

$tagName = $releaseInfo.tag_name
if (-not $tagName) { Throw-Terminate "GitHub response missing tag_name" }

$zipAsset = $releaseInfo.assets | Where-Object { $_.name -match '^service-v[\d\.]+\.zip$' }
if (-not $zipAsset) { Throw-Terminate "No matching zip found in release" }

$assetName = $zipAsset.name
$downloadUrl = $zipAsset.browser_download_url
$version = ($assetName -replace '^service-v([\d\.]+)\.zip$', '$1')
#endregion

#region Download and Extract
$tempDir = Join-Path $env:TEMP "TempMonitorDeploy"
$tempZip = Join-Path $tempDir "service-v$version.zip"
$extractDir = Join-Path $tempDir "extracted"
$installDir = Join-Path $installRoot "TemperatureMonitor-v$version"

if (Test-Path $tempDir) { Remove-Item -Path $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing -ErrorAction Stop
Expand-Archive -LiteralPath $tempZip -DestinationPath $extractDir -Force -ErrorAction Stop
#endregion

#region Install Files
$publishFolder = Get-ChildItem -Path $extractDir -Recurse -Directory |
Where-Object { $_.Name -eq 'publish_output' } | Select-Object -First 1

if (-not $publishFolder) { Throw-Terminate "publish_output not found in ZIP" }

if (Test-Path $installDir) { Remove-Item -Path $installDir -Recurse -Force -ErrorAction Stop }
New-Item -Path $installDir -ItemType Directory -Force | Out-Null
Copy-Item -Path (Join-Path $publishFolder.FullName '*') -Destination $installDir -Recurse -Force -ErrorAction Stop
#endregion

#region Register Service
$exePath = Join-Path $installDir $serviceExe
if (-not (Test-Path $exePath)) { Throw-Terminate "Service executable not found: $exePath" }

New-Service -Name $serviceName -BinaryPathName "`"$exePath`"" -DisplayName $serviceName -StartupType Manual -ErrorAction Stop
Write-Host "Service registered successfully."
#endregion

#region Cleanup
if ($Transcribing) { Stop-Transcript | Out-Null }
exit 0
#endregion
