param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationPath
)

$runner = Join-Path $PSScriptRoot "PatarikAIOS.Automation.exe"
if (-not (Test-Path -LiteralPath $runner)) {
    throw "Automation runner was not found: $runner"
}
if (-not (Test-Path -LiteralPath $ApplicationPath)) {
    throw "Patarik AI OS was not found: $ApplicationPath"
}

Start-Process -FilePath $runner -ArgumentList @('--app', ('"' + $ApplicationPath + '"')) -WindowStyle Hidden
