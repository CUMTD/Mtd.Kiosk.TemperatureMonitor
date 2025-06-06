<#
.SYNOPSIS
    Detects whether the MTD Temperature Monitor service is installed.
.NOTES
    Used by Intune as the detection script.
#>

$serviceName = 'MTD Temperature Monitor'


function Write-Log {
    param([string]$msg)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Output "[temp-detect][$timestamp] $msg"
}

Write-Log "Running detection for service: $serviceName"

try {
    $svc = Get-Service -Name $serviceName -ErrorAction Stop
    Write-Log "Service found: $($svc.DisplayName) [Status: $($svc.Status)]"
    exit 0
}
catch {
    Write-Log "Service '$serviceName' not found."
    exit 1
}
