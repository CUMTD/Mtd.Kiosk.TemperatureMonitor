<#
.SYNOPSIS
    Uninstalls the MTD Temperature Monitor service and deletes install folder.
.NOTES
    Used by Intune as the uninstall command.
#>

$logFolder = "C:\ProgramData\MTD"
$logPath = Join-Path $logFolder "TempMonitor_uninstall.log"

function Throw-Terminate {
    param([string]$Message, [int]$ExitCode = 1)
    Write-Error "ERROR: $Message"
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
    Write-Warning "Could not start transcript: $_"
}

$serviceName = 'MTD Temperature Monitor'
$installRoot = "$env:SystemDrive\Services"

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

$folders = Get-ChildItem -Path $installRoot -Directory -Filter 'TemperatureMonitor-v*' -ErrorAction SilentlyContinue
foreach ($dir in $folders) {
    Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction Stop
}

if ($script:Transcribing) { Stop-Transcript | Out-Null }
exit 0
