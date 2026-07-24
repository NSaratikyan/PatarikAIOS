param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationPath,
    [Parameter(Mandatory = $true)]
    [string]$AutomationPath
)

$ErrorActionPreference = 'Stop'

$taskName = 'Patarik AI OS Automation'
$starter = Join-Path $AutomationPath 'Start-PatarikAutomation.ps1'
if (-not (Test-Path -LiteralPath $starter)) { throw "Start script not found: $starter" }
if (-not (Test-Path -LiteralPath $ApplicationPath)) { throw "Application not found: $ApplicationPath" }

# Runs when the Windows account signs in. The Automation Host itself restarts
# Patarik AI OS after a crash, so this task has only one responsibility: start it.
$arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$starter`" -ApplicationPath `"$ApplicationPath`""
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -StartWhenAvailable
try {
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description 'Keeps Patarik AI OS Telegram automation running in the background.' -Force | Out-Null
}
catch {
    throw "Automation task was not installed. Run this script from PowerShell opened with 'Run as administrator'. Details: $($_.Exception.Message)"
}

Write-Host "Installed: $taskName"
Write-Host "The task will start automatically at the next Windows sign-in."
Write-Host "To start it now: run Start-PatarikAutomation.ps1 with the same ApplicationPath."
