param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'publish')
)

$root = Split-Path $PSScriptRoot -Parent
$config = Join-Path $root 'NuGet.Config'
$appProject = Join-Path $root 'PatarikAIOS.csproj'
$automationProject = Join-Path $PSScriptRoot 'PatarikAIOS.Automation.csproj'
$appOutput = Join-Path $OutputPath 'App'
$automationOutput = Join-Path $OutputPath 'Automation'

New-Item -ItemType Directory -Force -Path $appOutput, $automationOutput | Out-Null
dotnet publish $appProject -c Release -r win-x64 --self-contained false --configfile $config -o $appOutput
if ($LASTEXITCODE -ne 0) { throw 'Patarik AI OS publishing failed.' }

dotnet publish $automationProject -c Release -r win-x64 --self-contained false --configfile $config -o $automationOutput
if ($LASTEXITCODE -ne 0) { throw 'Automation Host publishing failed.' }

Copy-Item (Join-Path $PSScriptRoot 'Start-PatarikAutomation.ps1') $automationOutput -Force
Copy-Item (Join-Path $PSScriptRoot 'Install-LocalAutomation.ps1') $automationOutput -Force
Copy-Item (Join-Path $PSScriptRoot 'README.md') $automationOutput -Force

Write-Host "Ready. Application: $appOutput\PatarikAIOS.exe"
Write-Host "Automation:  $automationOutput"
Write-Host "Next, run Install-LocalAutomation.ps1 from the Automation folder."
